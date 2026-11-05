using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Desktop.Notifications;
using TMPublisher.Desktop.Startup;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Scheduler;

/// <summary>
/// The window's side of the background scheduler: after unlock it makes sure the scheduler runs (starts it when it does not, restarts one of
/// another version), hands over the database key of a master-password database (over the same-user local channel, never on the command line,
/// in the environment or in a log), tells it to read the schedule again after a change, and listens for its events while the window is open
/// ("posts changed", notifications, which this window then shows instead of the short-lived notification process).
/// </summary>
public sealed class SchedulerSupervisor(AppPaths paths, Vault vault, DatabaseKey key, ISchedulerLauncher launcher, ToastNotifier toasts, TimeProvider clock,
    ILogger<SchedulerSupervisor> logger) : IStartupTask, IDisposable
{
    private static readonly TimeSpan StartWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Reconnect = TimeSpan.FromSeconds(15);
    private readonly SchedulerClient _client = new(paths.DataDirectory);
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _ensure = new(1, 1);
    private Task? _watch;
    private int _disposed;

    /// <summary>The last known state.</summary>
    public SchedulerState State { get; private set; } = new() { Status = SchedulerStatus.NotRunning };

    /// <summary>Raised (on a pool thread) when <see cref="State"/> was read again.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised (on a pool thread) when the scheduler reports that posts changed state.</summary>
    public event EventHandler? PostsChanged;

    private static string OwnVersion => typeof(SchedulerSupervisor).Assembly.GetName().Version?.ToString(3) ?? string.Empty;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
        StartWatching();
    }

    /// <summary>Makes sure the scheduler runs with the open database; returns its state (with the reason when it could not be started).</summary>
    public async Task<SchedulerState> EnsureRunningAsync(CancellationToken cancellationToken)
    {
        await _ensure.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await RefreshAsync(cancellationToken).ConfigureAwait(false);
            if (state.Status != SchedulerStatus.NotRunning && state.Version != OwnVersion)
            {
                logger.LogWarning("The background scheduler {Running} runs, this window is {Own}; starting the matching one", state.Version, OwnVersion);
                if (await StopAndWaitAsync(cancellationToken).ConfigureAwait(false))
                {
                    state = State;
                }
            }

            if (state.Status == SchedulerStatus.NotRunning)
            {
                if (!launcher.TryStart(out var reason))
                {
                    logger.LogError("The background scheduler could not be started: {Reason}", reason);
                    Publish(new SchedulerState { Status = SchedulerStatus.NotRunning, Message = reason });
                    return State;
                }

                logger.LogInformation("Background scheduler started");
                state = await WaitForAsync(s => s.Status is not (SchedulerStatus.NotRunning or SchedulerStatus.Starting), StartWait, cancellationToken)
                    .ConfigureAwait(false);
            }

            return await HandOverAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ensure.Release();
        }
    }

    /// <summary>Reads the state again.</summary>
    public async Task<SchedulerState> RefreshAsync(CancellationToken cancellationToken)
    {
        Publish(await _client.StatusAsync(cancellationToken).ConfigureAwait(false));
        return State;
    }

    /// <summary>Asks the scheduler to read the schedule again; starts it when it does not answer. Never waits for sending.</summary>
    public void Wake() => _ = WakeAsync();

    /// <summary>Pauses sending (kept across restarts of the scheduler).</summary>
    public Task<SchedulerReply> PauseAsync(CancellationToken cancellationToken) => CommandAsync(SchedulerCommands.Pause, cancellationToken);

    /// <summary>Resumes sending.</summary>
    public Task<SchedulerReply> ResumeAsync(CancellationToken cancellationToken) => CommandAsync(SchedulerCommands.Resume, cancellationToken);

    /// <summary>
    /// Ends the scheduler process when no post is being sent and waits until it is gone; <c>false</c> when a post is being sent or it did not
    /// end in time (the reason is in <see cref="State"/> and the log).
    /// </summary>
    public async Task<bool> StopAndWaitAsync(CancellationToken cancellationToken)
    {
        var reply = await _client.SendAsync(SchedulerCommands.Quit, null, cancellationToken).ConfigureAwait(false);
        if (!reply.Reached)
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (reply.Text == SchedulerCommands.Busy)
        {
            logger.LogInformation("The background scheduler is sending a post; it was not stopped");
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var state = await WaitForAsync(s => s.Status == SchedulerStatus.NotRunning, StopWait, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Background scheduler {Result}", state.Status == SchedulerStatus.NotRunning ? "stopped" : "did not stop in time");
        return state.Status == SchedulerStatus.NotRunning;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Registered as itself and as a startup task: the container disposes the one instance twice.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stop.Cancel();
        _stop.Dispose();
        _ensure.Dispose();
    }

    private async Task<SchedulerState> HandOverAsync(SchedulerState state, CancellationToken cancellationToken)
    {
        if (vault.State != VaultState.Open)
        {
            return state;
        }

        if (state.Status == SchedulerStatus.NoDatabase)
        {
            // The first start was just finished in this window.
            var reply = await _client.SendAsync(SchedulerCommands.Reload, null, cancellationToken).ConfigureAwait(false);
            Log("open the new database", reply);
            return await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        if (state.Status == SchedulerStatus.WaitingForKey)
        {
            var dek = key.Copy();
            SchedulerReply reply;
            try
            {
                reply = await _client.SendAsync(SchedulerCommands.Key, Convert.ToBase64String(dek), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dek);
            }

            Log("take the database key", reply);
            return await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        return state;
    }

    private void Log(string what, SchedulerReply reply)
    {
        if (reply.IsOk)
        {
            logger.LogInformation("The background scheduler was asked to {What}: done", what);
        }
        else
        {
            logger.LogError("The background scheduler was asked to {What}: {Answer}", what, reply.Text);
        }
    }

    private async Task WakeAsync()
    {
        try
        {
            var reply = await _client.SendAsync(SchedulerCommands.Wake, null, CancellationToken.None).ConfigureAwait(false);
            if (!reply.Reached)
            {
                logger.LogWarning("The background scheduler did not answer the wake-up ({Reason}); starting it", reply.Text);
                await EnsureRunningAsync(CancellationToken.None).ConfigureAwait(false);
                StartWatching();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Waking the background scheduler failed");
        }
    }

    private async Task<SchedulerReply> CommandAsync(string command, CancellationToken cancellationToken)
    {
        var reply = await _client.SendAsync(command, null, cancellationToken).ConfigureAwait(false);
        Log(command, reply);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        return reply;
    }

    private async Task<SchedulerState> WaitForAsync(Func<SchedulerState, bool> done, TimeSpan limit, CancellationToken cancellationToken)
    {
        var until = clock.GetUtcNow() + limit;
        var state = await RefreshAsync(cancellationToken).ConfigureAwait(false);
        while (!done(state) && clock.GetUtcNow() < until)
        {
            await Task.Delay(Poll, clock, cancellationToken).ConfigureAwait(false);
            state = await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        return state;
    }

    private void StartWatching()
    {
        if (_watch is null || _watch.IsCompleted)
        {
            _watch = WatchAsync(_stop.Token);
        }
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var reason = await _client.WatchAsync(OnEvent, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            logger.LogInformation("Event connection to the background scheduler ended: {Reason}", reason);
            try
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(Reconnect, clock, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnEvent(string line)
    {
        if (line == SchedulerCommands.Changed)
        {
            PostsChanged?.Invoke(this, EventArgs.Empty);
            _ = RefreshAsync(CancellationToken.None);
            return;
        }

        try
        {
            if (SchedulerChannel.ParseNotification(line) is { } message)
            {
                toasts.ShowFromScheduler(new AppNotification(message.Kind, message.Title, message.Message), message.Seconds);
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "An event of the background scheduler could not be read");
        }
    }

    private void Publish(SchedulerState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
