using Microsoft.Extensions.Logging;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Triggers;
using TMPublisher.Desktop.Startup;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Sending;

/// <summary>
/// The open window is the sender of its data folder: after unlock it takes the sender lock (<see cref="SenderGate"/>; while a run the system
/// scheduler started is still sending, it tries again every few seconds) and runs the schedule's timer (<see cref="PostScheduler"/>). After every
/// pass, and when the window closes, it sets the system scheduler's next start (<see cref="TriggerArmer"/>), so the posts go out while it is
/// closed too. Closing lets a post being sent finish (without a window), then gives the lock back.
/// </summary>
public sealed class WindowSender : IStartupTask, IDisposable
{
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HoldPoll = TimeSpan.FromMilliseconds(500);
    private readonly SenderGate _gate;
    private readonly PostScheduler _scheduler;
    private readonly TriggerArmer _armer;
    private readonly PauseSwitch _pause;
    private readonly LegacyCleanup _legacy;
    private readonly Vault _vault;
    private readonly TimeProvider _clock;
    private readonly ILogger<WindowSender> _logger;
    private readonly SemaphoreSlim _start = new(1, 1);
    private ITimer? _retry;
    private bool _started;
    private bool _everStarted;
    private bool _waitLogged;
    private bool _active;
    private int _closing;
    private int _disposed;

    /// <summary>Creates.</summary>
    public WindowSender(SenderGate gate, PostScheduler scheduler, TriggerArmer armer, PauseSwitch pause, LegacyCleanup legacy, Vault vault,
        TimeProvider clock, ILogger<WindowSender> logger)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _gate = gate;
        _scheduler = scheduler;
        _armer = armer;
        _pause = pause;
        _legacy = legacy;
        _vault = vault;
        _clock = clock;
        _logger = logger;
        _scheduler.PassCompleted += (_, _) => _ = ArmAsync(_scheduler.RetryAfter);
    }

    /// <summary>How long closing waits for a post being sent before it says so in the log (and the longest wait when the system ends).</summary>
    internal TimeSpan QuitWait { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The longest wait for a post being sent when the window closed (as long as a run may take): the program stays without a window until
    /// the post is sent, so an upload is never cut off half way.
    /// </summary>
    internal TimeSpan SendWait { get; set; } = TriggerLimits.RunTimeLimit;

    /// <summary>Raised (on a pool thread) when what <see cref="IsSending"/>, <see cref="IsPaused"/> or <see cref="Trigger"/> show may have changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Whether this window sends now (it holds the lock and its timer runs).</summary>
    public bool IsSending => _started;

    /// <summary>Whether sending is paused (here and in the runs while the window is closed).</summary>
    public bool IsPaused => _pause.IsPaused;

    /// <summary>The earliest scheduled post the last pass found.</summary>
    public DateTimeOffset? NextPostUtc => _scheduler.NextPostUtc;

    /// <summary>The system scheduler's entry as it was written last.</summary>
    public TriggerRecord Trigger => _armer.Current;

    /// <summary>The entry's name in the system scheduler.</summary>
    public string TriggerName => _armer.Name;

    /// <summary>The system scheduler's kind.</summary>
    public string TriggerKind => _armer.Kind;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _legacy.Run(mentionOldProgram: true);
        _active = true;
        await TryStartAsync().ConfigureAwait(false);
        if (!_started)
        {
            // The pass of the running sender sets the next start too; this one covers a window that waits.
            await ArmAsync(null).ConfigureAwait(false);
        }
    }

    /// <summary>Reads the schedule again (a post was saved, a decision taken, a setting changed); never waits for sending.</summary>
    public void Wake()
    {
        if (_started)
        {
            _scheduler.Wake();
            return;
        }

        // Before the main window started sending (first start, unlock) only the system scheduler's entry is set.
        if (_active)
        {
            _ = TryStartAsync();
        }

        _ = ArmAsync(null);
    }

    /// <summary>Pauses or resumes sending (kept in the data folder, so the runs while the window is closed pause too).</summary>
    public async Task SetPausedAsync(bool paused)
    {
        _pause.Set(paused);
        if (_started)
        {
            if (paused)
            {
                _scheduler.Pause();
            }
            else
            {
                _scheduler.Resume();
            }
        }

        _logger.LogInformation("Scheduling {State} by the user", paused ? "paused" : "resumed");
        await ArmAsync(null).ConfigureAwait(false);
    }

    /// <summary>
    /// Before the database is replaced (restore): this window must be the sender and must not be sending. <c>false</c> when a post is being sent
    /// (here, or by a run that does not end within <paramref name="wait"/>).
    /// </summary>
    public async Task<bool> StopForRestoreAsync(TimeSpan wait)
    {
        await _start.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_started)
            {
                if (!await _scheduler.StopAsync(onlyIfIdle: true, CancellationToken.None).ConfigureAwait(false))
                {
                    return false;
                }

                _started = false;
                _logger.LogInformation("Sending stopped for a restore");
                return true;
            }

            var until = _clock.GetUtcNow() + wait;
            while (!await _gate.TryTakeAsync(TriggerLimits.OldSchedulerWait, CancellationToken.None).ConfigureAwait(false))
            {
                if (_clock.GetUtcNow() >= until)
                {
                    _logger.LogInformation("A run is still sending; the restore waits for it");
                    return false;
                }

                await Task.Delay(HoldPoll, _clock).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _start.Release();
        }
    }

    /// <summary>After a restore (or a failed one): sending starts again on the database that is there now.</summary>
    public async Task RestartAsync()
    {
        if (_active)
        {
            await TryStartAsync().ConfigureAwait(false);
        }

        await ArmAsync(null).ConfigureAwait(false);
    }

    /// <summary>
    /// The window closed: no new post is taken; a post being sent is finished (the program stays without a window until it is sent, at most
    /// <see cref="SendWait"/>; only <see cref="QuitWait"/> when the system is shutting down or signing out, otherwise the next sender shows it
    /// as stopped), then the next start is set and the lock is given back.
    /// </summary>
    public void Close(bool systemEnding = false)
    {
        if (Interlocked.Exchange(ref _closing, 1) == 1)
        {
            return;
        }

        _retry?.Dispose();
        try
        {
            if (_started)
            {
                var stop = _scheduler.StopAsync(onlyIfIdle: false, CancellationToken.None);
                if (!stop.Wait(QuitWait))
                {
                    if (systemEnding)
                    {
                        _logger.LogWarning("A post was still being sent after {Wait} while the system ends; closing anyway (the next sender shows it as stopped)", QuitWait);
                    }
                    else
                    {
                        _logger.LogInformation("A post is still being sent; the program stays open without a window until it is sent (at most {Wait})", SendWait);
                        if (stop.Wait(SendWait > QuitWait ? SendWait - QuitWait : TimeSpan.Zero))
                        {
                            _logger.LogInformation("The post being sent when the window closed is finished");
                        }
                        else
                        {
                            _logger.LogWarning("A post was still being sent after {Wait}; closing anyway (the next sender shows it as stopped)", SendWait);
                        }
                    }
                }

                _started = false;
            }

            if (_vault.State == VaultState.Open && !_armer.ArmAsync(null, CancellationToken.None).Wait(QuitWait))
            {
                _logger.LogWarning("Setting the system scheduler's next start took longer than {Wait} while closing", QuitWait);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Closing the sender failed");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Registered as itself and as a startup task: the container disposes the one instance twice.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _retry?.Dispose();
        _start.Dispose();
    }

    private async Task TryStartAsync()
    {
        if (Volatile.Read(ref _closing) == 1 || _vault.State != VaultState.Open)
        {
            return;
        }

        await _start.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            if (!await _gate.TryTakeAsync(TriggerLimits.OldSchedulerWait, CancellationToken.None).ConfigureAwait(false))
            {
                if (!_waitLogged)
                {
                    _waitLogged = true;
                    _logger.LogInformation("Another sender works on this data folder (a run the system scheduler started); this window sends once it ends");
                }

                _retry?.Dispose();
                _retry = _clock.CreateTimer(_ => _ = TryStartAsync(), null, Retry, Timeout.InfiniteTimeSpan);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            _retry?.Dispose();
            _retry = null;
            _waitLogged = false;

            // Sending from here on (the first pass may already send a due post): closing now waits for it like for any other.
            _started = true;
            try
            {
                if (_everStarted)
                {
                    await _scheduler.StartAgainAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await _scheduler.StartAsync(CancellationToken.None, _pause.IsPaused).ConfigureAwait(false);
                }
            }
            catch
            {
                _started = false;
                throw;
            }

            _everStarted = true;
            _logger.LogInformation("This window sends the scheduled posts while it is open{Paused}", _pause.IsPaused ? " (paused)" : string.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sending could not be started in this window; trying again in {Wait}", Retry);
            _retry?.Dispose();
            _retry = _clock.CreateTimer(_ => _ = TryStartAsync(), null, Retry, Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _start.Release();
        }
    }

    private async Task ArmAsync(TimeSpan? retryAfter)
    {
        if (_vault.State != VaultState.Open || Volatile.Read(ref _closing) == 1)
        {
            return;
        }

        await _armer.ArmAsync(retryAfter, CancellationToken.None).ConfigureAwait(false);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
