using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Sending;

/// <summary>
/// One sender per data folder: the open window or one run the system scheduler started, never both and never two runs. It is the
/// <see cref="InstanceLock.Sender"/> lock file, which the system releases when the process ends, also when it is killed. The always-running
/// scheduler of 0.1.3–0.1.4 held the same file: when it still runs after an update, it is asked to end (and given a moment to finish a post).
/// </summary>
public sealed class SenderGate(AppPaths paths, TimeProvider clock, ILogger<SenderGate> logger) : IDisposable
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan AskAgain = TimeSpan.FromSeconds(5);
    private readonly Lock _gate = new();
    private InstanceLock? _held;

    /// <summary>Whether this process is the sender.</summary>
    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                return _held is not null;
            }
        }
    }

    /// <summary>
    /// Takes the lock. When another process holds it: a 0.1.3–0.1.4 scheduler of the folder is asked to end and waited for, at most
    /// <paramref name="oldSchedulerWait"/>; a window or a run of this version is not waited for (<c>false</c> at once).
    /// </summary>
    public async Task<bool> TryTakeAsync(TimeSpan oldSchedulerWait, CancellationToken cancellationToken)
    {
        if (TryTake())
        {
            return true;
        }

        var answer = await OldScheduler.AskToQuitAsync(paths.DataDirectory, cancellationToken).ConfigureAwait(false);
        if (answer is OldSchedulerAnswer.NotRunning or OldSchedulerAnswer.Unexpected)
        {
            return false;
        }

        logger.LogWarning("The always-running scheduler of TM Publisher 0.1.3-0.1.4 still runs on this data folder; it was asked to end ({Answer})", answer);
        var until = clock.GetUtcNow() + oldSchedulerWait;
        var asked = clock.GetUtcNow();
        while (clock.GetUtcNow() < until)
        {
            await Task.Delay(Poll, clock, cancellationToken).ConfigureAwait(false);
            if (TryTake())
            {
                logger.LogInformation("The 0.1.3-0.1.4 scheduler has ended; this version sends from now on");
                return true;
            }

            if (clock.GetUtcNow() - asked >= AskAgain)
            {
                asked = clock.GetUtcNow();
                answer = await OldScheduler.AskToQuitAsync(paths.DataDirectory, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogWarning("The 0.1.3-0.1.4 scheduler did not end within {Wait} (last answer: {Answer}); trying again later", oldSchedulerWait, answer);
        return false;
    }

    /// <summary>Gives the lock back (another run or the window may send then).</summary>
    public void Release()
    {
        lock (_gate)
        {
            _held?.Dispose();
            _held = null;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Release();

    private bool TryTake()
    {
        lock (_gate)
        {
            _held ??= InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Sender, out _);
            return _held is not null;
        }
    }
}
