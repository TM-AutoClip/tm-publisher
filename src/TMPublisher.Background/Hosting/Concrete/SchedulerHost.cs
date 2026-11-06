using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Ipc;
using TMPublisher.Background.Localization;
using TMPublisher.Background.Os;
using TMPublisher.Core.Settings;
using TMPublisher.Core.Security;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Hosting;

/// <summary>
/// The life of the background scheduler: opens the database (automatically with the keyring, or with the key the window hands over for a
/// master-password database: the scheduler cannot ask for the password, so it waits), starts <see cref="PostScheduler"/>, carries out the
/// window's requests and the tray menu, keeps "pause" across restarts (a <c>scheduler-paused.txt</c> file in the data folder) and ends cleanly.
/// </summary>
public sealed class SchedulerHost(AppPaths paths, Vault vault, PostScheduler scheduler, ISettingsStore settings, TextCatalog text, IPowerEvents power,
    ITrayIcon tray, IMainLoop loop, WatcherHub watchers, TimeProvider clock, ILogger<SchedulerHost> logger) : IDisposable
{
    private static readonly TimeSpan QuietMemoryLog = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim _open = new(1, 1);
    private SchedulerStatus _status = SchedulerStatus.Starting;
    private string _message = string.Empty;
    private bool _schedulerStarted;
    private int _quitting;
    private ITimer? _retry;
    private ITimer? _memory;

    private string PauseFile => Path.Combine(paths.DataDirectory, "scheduler-paused.txt");

    /// <summary>The state the window and the tray show.</summary>
    public SchedulerState State => new()
    {
        Version = typeof(SchedulerHost).Assembly.GetName().Version?.ToString(3) ?? string.Empty,
        Status = _status,
        Sending = scheduler.IsSending,
        NextPostUtc = scheduler.NextPostUtc,
        Message = _message,
        ProcessId = Environment.ProcessId,
    };

    /// <summary>Connects the events and opens the database (or waits for it).</summary>
    public async Task StartAsync()
    {
        try
        {
            loop.StopRequested += (_, _) => _ = QuitAsync(onlyIfIdle: false, StopWait);
            tray.OpenRequested += (_, _) => OpenWindow();
            tray.PauseToggled += (_, _) => _ = SetPausedAsync(_status != SchedulerStatus.Paused);
            tray.QuitRequested += (_, _) => _ = QuitAsync(onlyIfIdle: false, Timeout.InfiniteTimeSpan);
            scheduler.AttentionChanged += (_, _) => _ = watchers.BroadcastAsync(SchedulerCommands.Changed);
            power.ScheduleStale += (_, _) => scheduler.Wake();
            power.Resumed += (_, _) => scheduler.WakeAfterResume();
            try
            {
                await power.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Not fatal: without these events the schedule is still read again every few minutes.
                logger.LogWarning(ex, "Could not listen for resume / clock events; the schedule is still read again every few minutes");
            }

            await OpenAsync().ConfigureAwait(false);
            _memory = clock.CreateTimer(_ => MemoryUse.Log(logger, "idle after start"), null, QuietMemoryLog, Timeout.InfiniteTimeSpan);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The scheduler could not start");
            SetStatus(SchedulerStatus.Error, ex.Message);
        }
    }

    /// <summary>Carries out one request of the window; returns the answer line. The key argument is never logged.</summary>
    public async Task<string> HandleAsync(string command, string? argument)
    {
        switch (command)
        {
            case SchedulerCommands.Status:
                return SchedulerChannel.StateJson(State);
            case SchedulerCommands.Wake:
                if (_status is SchedulerStatus.Running or SchedulerStatus.Paused)
                {
                    await UseLanguageAsync().ConfigureAwait(false);
                    scheduler.Wake();
                }
                else
                {
                    await OpenAsync().ConfigureAwait(false);
                }

                return SchedulerCommands.Ok;
            case SchedulerCommands.Reload:
                return await ReloadAsync().ConfigureAwait(false);
            case SchedulerCommands.Key:
                return await TakeKeyAsync(argument).ConfigureAwait(false);
            case SchedulerCommands.Pause:
                return await SetPausedAsync(true).ConfigureAwait(false);
            case SchedulerCommands.Resume:
                return await SetPausedAsync(false).ConfigureAwait(false);
            case SchedulerCommands.Quit:
                // Answered from the stop itself, not from a look at "sending": a pass that took the schedule a moment ago is either told to
                // claim nothing more (and ends) or still sends after a short wait, which is "busy". Once stopped, nothing can be cut off.
                if (!await scheduler.StopAsync(onlyIfIdle: true, CancellationToken.None).ConfigureAwait(false))
                {
                    return SchedulerCommands.Busy;
                }

                _ = QuitAsync(onlyIfIdle: true, StopWait);
                return SchedulerCommands.Ok;
            default:
                logger.LogWarning("Unknown request '{Command}' on the scheduler channel", command.Length > 20 ? command[..20] : command);
                return SchedulerCommands.Error + " unknown request";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _retry?.Dispose();
        _memory?.Dispose();
        _open.Dispose();
    }

    private async Task OpenAsync()
    {
        await _open.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_status is SchedulerStatus.Running or SchedulerStatus.Paused)
            {
                return;
            }

            switch (vault.State)
            {
                case VaultState.NotCreated:
                    SetStatus(SchedulerStatus.NoDatabase, string.Empty);
                    logger.LogInformation("No database yet; waiting until the window finishes the first start");
                    return;
                case VaultState.Locked when vault.Method == KeyMethods.Password:
                    SetStatus(SchedulerStatus.WaitingForKey, string.Empty);
                    logger.LogInformation("The database is protected with a master password; waiting until the window is unlocked and hands the key over");
                    return;
                case VaultState.Locked:
                    try
                    {
                        await vault.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // The keyring may not be unlocked yet right after signing in: tried again at the next wake-up or in a few minutes.
                        logger.LogWarning(ex, "The database could not be opened automatically; trying again in {Wait}", SchedulerLimits.SafetyInterval);
                        SetStatus(SchedulerStatus.Error, Redactor.Redact(ex.Message));
                        _retry?.Dispose();
                        _retry = clock.CreateTimer(_ => _ = OpenAsync(), null, SchedulerLimits.SafetyInterval, Timeout.InfiniteTimeSpan);
                        return;
                    }

                    break;
            }

            await StartSchedulerAsync().ConfigureAwait(false);
        }
        finally
        {
            _open.Release();
        }
    }

    private async Task StartSchedulerAsync()
    {
        await UseLanguageAsync().ConfigureAwait(false);
        var paused = File.Exists(PauseFile);
        if (_schedulerStarted)
        {
            if (paused)
            {
                scheduler.Pause();
            }

            await scheduler.ReloadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            await scheduler.StartAsync(CancellationToken.None, paused).ConfigureAwait(false);
            _schedulerStarted = true;
        }

        SetStatus(paused ? SchedulerStatus.Paused : SchedulerStatus.Running, string.Empty);
        logger.LogInformation("Scheduler running{Paused}; next post {Next}", paused ? " (paused)" : string.Empty,
            scheduler.NextPostUtc is { } next ? next.ToString("u", System.Globalization.CultureInfo.InvariantCulture) : "none");
        _ = watchers.BroadcastAsync(SchedulerCommands.Changed);
    }

    private async Task<string> ReloadAsync()
    {
        // The window finished the first start or changed how the key is protected. A running scheduler keeps its key (it does not change);
        // a restore replaces the database only while the scheduler is stopped: both restore paths of the window (Settings and the unlock /
        // first-start window) stop it first and start it again.
        logger.LogInformation("The window asked to open the database again");
        if (_status is SchedulerStatus.Running or SchedulerStatus.Paused)
        {
            scheduler.Wake();
            return SchedulerCommands.Ok;
        }

        await OpenAsync().ConfigureAwait(false);
        return _status is SchedulerStatus.Running or SchedulerStatus.Paused or SchedulerStatus.WaitingForKey or SchedulerStatus.NoDatabase
            ? SchedulerCommands.Ok
            : SchedulerCommands.Error + " " + _message;
    }

    private async Task<string> TakeKeyAsync(string? argument)
    {
        if (_status is SchedulerStatus.Running or SchedulerStatus.Paused)
        {
            return SchedulerCommands.Ok;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(argument ?? string.Empty);
        }
        catch (FormatException)
        {
            logger.LogWarning("The key handed over by the window is not valid base64");
            return SchedulerCommands.Error + " the key is not valid";
        }

        await _open.WaitAsync().ConfigureAwait(false);
        try
        {
            await vault.OpenWithKeyAsync(key, CancellationToken.None).ConfigureAwait(false);
            await StartSchedulerAsync().ConfigureAwait(false);
            return SchedulerCommands.Ok;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The database did not open with the key handed over by the window");
            return SchedulerCommands.Error + " " + Redactor.Redact(ex.Message);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            _open.Release();
        }
    }

    private async Task<string> SetPausedAsync(bool paused)
    {
        if (_status is not (SchedulerStatus.Running or SchedulerStatus.Paused))
        {
            return SchedulerCommands.Error + " the scheduler is not running";
        }

        try
        {
            if (paused)
            {
                await File.WriteAllTextAsync(PauseFile, "Scheduling was paused in TM Publisher. Delete this file or choose Resume to send posts again.\n")
                    .ConfigureAwait(false);
                scheduler.Pause();
            }
            else
            {
                File.Delete(PauseFile);
                scheduler.Resume();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The pause state could not be stored");
            return SchedulerCommands.Error + " " + ex.Message;
        }

        SetStatus(paused ? SchedulerStatus.Paused : SchedulerStatus.Running, string.Empty);
        _ = watchers.BroadcastAsync(SchedulerCommands.Changed);
        return SchedulerCommands.Ok;
    }

    private async Task QuitAsync(bool onlyIfIdle, TimeSpan wait)
    {
        if (Interlocked.Exchange(ref _quitting, 1) == 1)
        {
            return;
        }

        try
        {
            logger.LogInformation("Quitting{Wait}", scheduler.IsSending ? " after the post being sent" : string.Empty);
            var stop = scheduler.StopAsync(onlyIfIdle, CancellationToken.None);
            if (await Task.WhenAny(stop, Task.Delay(wait, clock)).ConfigureAwait(false) != stop)
            {
                logger.LogWarning("A post was still being sent after {Wait}; ending anyway (it waits for the user at the next start)", wait);
            }
            else if (!await stop.ConfigureAwait(false))
            {
                logger.LogInformation("Not quitting: a post is being sent");
                Interlocked.Exchange(ref _quitting, 0);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stopping the scheduler failed; ending anyway");
        }

        tray.Remove();
        loop.Quit();
    }

    private void OpenWindow()
    {
        if (ProgramFiles.Window is not { } window)
        {
            logger.LogError("The window program was not found next to the scheduler");
            return;
        }

        var start = new ProcessStartInfo(window) { UseShellExecute = false };
        if (!paths.IsDefaultDirectory)
        {
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(paths.DataDirectory);
        }

        try
        {
            using var process = Process.Start(start);
            logger.LogInformation("Window opened from the tray");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "The window could not be opened");
        }
    }

    private async Task UseLanguageAsync()
    {
        try
        {
            text.Use((await settings.GetAsync(CancellationToken.None).ConfigureAwait(false)).Language);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The language setting could not be read; English is used");
        }
    }

    private void SetStatus(SchedulerStatus status, string message)
    {
        _status = status;
        _message = message;
        tray.Update(State);
    }
}
