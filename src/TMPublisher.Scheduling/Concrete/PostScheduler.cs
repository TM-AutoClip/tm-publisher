using Microsoft.Extensions.Logging;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;

namespace TMPublisher.Scheduling;

/// <summary>
/// One timer for the whole schedule (it runs in the background scheduler process, the only place posts are sent from). The timer is set to the
/// earliest scheduled post, at most <see cref="SchedulerLimits.SafetyInterval"/> ahead, and set again whenever something changes. No loop, no
/// polling in between. A due post is first claimed in the database (scheduled → publishing, only if nobody changed it meanwhile) and then read
/// again and sent, so an edit, a move or a second scheduler can never make it go out twice or with old content. Posts are sent one at a time.
/// A post found more than five minutes late (the computer was off or asleep, scheduling was paused) is not sent on its own: it becomes
/// "missed" and waits for the user.
/// </summary>
public sealed class PostScheduler : IDisposable
{
    private readonly IPostStore _posts;
    private readonly PostSender _sender;
    private readonly ProcessingWatcher _watcher;
    private readonly INotifier _notifier;
    private readonly ILocalizer _text;
    private readonly TimeProvider _clock;
    private readonly ILogger<PostScheduler> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _timerGate = new();
    private ITimer? _timer;
    private bool _started;
    private bool _stopped;
    private volatile bool _paused;

    /// <summary>Creates.</summary>
    public PostScheduler(IPostStore posts, PostSender sender, ProcessingWatcher watcher, INotifier notifier, ILocalizer text, TimeProvider clock,
        ILogger<PostScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _posts = posts;
        _sender = sender;
        _watcher = watcher;
        _notifier = notifier;
        _text = text;
        _clock = clock;
        _logger = logger;
        _sender.Processing += (_, item) => _watcher.Watch(item.Post.Id, item.Target.Id);
    }

    /// <summary>Raised when posts changed state (sent, stopped, blocked, missed): the window reads its lists again.</summary>
    public event EventHandler? AttentionChanged;

    /// <summary>Whether the computer has a network connection now (replaced in tests).</summary>
    internal Func<bool> NetworkAvailable { get; set; } = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable;

    /// <summary>When the timer fires next (UTC); <c>null</c> before the start, while paused and after the stop.</summary>
    public DateTimeOffset? NextWakeUtc { get; private set; }

    /// <summary>Time of the earliest scheduled post (UTC) found at the last reading; <c>null</c> when none.</summary>
    public DateTimeOffset? NextPostUtc { get; private set; }

    /// <summary>Whether a post is being sent now.</summary>
    public bool IsSending { get; private set; }

    /// <summary>Whether sending is paused by the user (nothing is sent or marked missed until it is resumed).</summary>
    public bool IsPaused => _paused;

    /// <summary>
    /// Starts: posts left "publishing" by a closed or killed scheduler become "stopped" (the finished targets are known from the local record),
    /// the processing follow-ups resume, due posts are handled and the timer is set. <paramref name="paused"/>: start without sending.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken, bool paused = false)
    {
        _paused = paused;
        await RecoverAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
        await RunDueAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The database was replaced or opened again (restore, a key handed over): forgets the old timer and follow-ups, then does what a start
    /// does on the data. No effect before the start.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (!_started || _stopped)
        {
            return;
        }

        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
        }

        _watcher.StopAll();
        _logger.LogInformation("Reading the schedule again from the database");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RecoverAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        AttentionChanged?.Invoke(this, EventArgs.Empty);
        await RunDueAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-reads the schedule (a post was saved in the window, the clock changed). No effect before the start.</summary>
    public void Wake()
    {
        if (_started && !_stopped)
        {
            _ = TickAsync();
        }
    }

    /// <summary>
    /// The computer woke up: the due posts are sent once the network is back (a short settle wait, then at most
    /// <see cref="SchedulerLimits.ResumeNetworkWait"/>), with their lateness measured from the moment of waking. Sending at once would fail
    /// on a Wi-Fi that is still reconnecting and stop the post.
    /// </summary>
    public void WakeAfterResume()
    {
        if (_started && !_stopped)
        {
            _ = ResumeAsync(_clock.GetUtcNow());
        }
    }

    /// <summary>Pauses sending: due posts wait (and become "missed" when resumed more than five minutes late).</summary>
    public void Pause()
    {
        _paused = true;
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
        }

        _logger.LogInformation("Scheduling paused by the user");
    }

    /// <summary>Resumes sending and handles what is due now.</summary>
    public void Resume()
    {
        _paused = false;
        _logger.LogInformation("Scheduling resumed by the user");
        Wake();
    }

    /// <summary>
    /// Stops for good (the scheduler process is quitting): waits until the post being sent is finished, then no timer fires again. Returns
    /// <c>false</c> without stopping when <paramref name="onlyIfIdle"/> and a post is being sent.
    /// </summary>
    public async Task<bool> StopAsync(bool onlyIfIdle, CancellationToken cancellationToken)
    {
        if (onlyIfIdle && IsSending)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _stopped = true;
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
        }

        _watcher.StopAll();
        _gate.Release();
        _logger.LogInformation("Scheduler stopped");
        return true;
    }

    /// <summary>Handles everything that is due now and sets the timer (also what the timer runs).</summary>
    public Task RunDueAsync(CancellationToken cancellationToken) => RunDueAsync(null, cancellationToken);

    /// <summary>Waits after waking until the network is there (bounded), then handles the due posts.</summary>
    internal async Task ResumeAsync(DateTimeOffset resumedAtUtc)
    {
        await Task.Delay(SchedulerLimits.ResumeSettle, _clock).ConfigureAwait(false);
        var waited = SchedulerLimits.ResumeSettle;
        while (!NetworkAvailable() && waited < SchedulerLimits.ResumeNetworkWait)
        {
            await Task.Delay(SchedulerLimits.ResumeNetworkCheck, _clock).ConfigureAwait(false);
            waited += SchedulerLimits.ResumeNetworkCheck;
        }

        if (!NetworkAvailable())
        {
            _logger.LogWarning("No network connection {Waited} after waking up; handling the due posts anyway", waited);
        }

        await TickAsync(resumedAtUtc).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        _gate.Dispose();
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        // Only one scheduler runs per data folder (its lock file), so a post still "publishing" here was left by a scheduler that ended.
        foreach (var post in await _posts.ListByStatusAsync([PostStatus.Publishing], cancellationToken).ConfigureAwait(false))
        {
            foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Publishing))
            {
                target.Status = TargetStatus.Failed;
                target.Message = _text["problem.interrupted"];
            }

            post.Status = PostStatus.Stopped;
            await _posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Post {PostId} was interrupted when the scheduler ended; waiting for the user", post.Id);
        }

        await _watcher.ResumeAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunDueAsync(DateTimeOffset? lateFromUtc, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var missed = 0;
        var changed = false;
        var refused = new HashSet<Guid>();
        var stuck = false;

        // Lateness is measured from the moment this run started (or the computer woke up): posts that fall due while an earlier post is
        // uploading, or while the network reconnects after sleep, are sent, not "missed".
        var runStart = lateFromUtc is { } from && from < _clock.GetUtcNow() ? from : _clock.GetUtcNow();
        try
        {
            if (_stopped)
            {
                return;
            }

            while (!_paused && await _posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false) is { ScheduledAtUtc: { } at } post && at <= _clock.GetUtcNow())
            {
                if (!refused.Add(post.Id))
                {
                    // The claim of this post failed once in this run and it is still listed as due: never loop on it; the next run tries again.
                    _logger.LogError("Post {PostId} is due but could not be claimed twice in one run; trying again in {Wait}", post.Id, SchedulerLimits.ClaimRetry);
                    stuck = true;
                    break;
                }

                var late = runStart - at > SchedulerLimits.LateTolerance;
                var claim = new PostClaim(post.Id, PostStatus.Scheduled, at, late ? PostStatus.Missed : PostStatus.Publishing);
                if (!await _posts.TryMoveAsync(claim, cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogInformation("Post {PostId} changed before it could be claimed (edited, moved or canceled); reading the schedule again", post.Id);
                    continue;
                }

                changed = true;
                if (late)
                {
                    _logger.LogWarning("Post {PostId} missed its time ({Late} late); waiting for the user", post.Id, runStart - at);
                    missed++;
                    continue;
                }

                // Claimed: read again, so exactly what is stored now is sent.
                if (await _posts.GetAsync(post.Id, cancellationToken).ConfigureAwait(false) is not { } claimed)
                {
                    _logger.LogWarning("Post {PostId} was deleted right after it was claimed; nothing sent", post.Id);
                    continue;
                }

                IsSending = true;
                try
                {
                    await _sender.SendAsync(claimed, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    IsSending = false;
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        if (missed > 0)
        {
            _notifier.Notify(new AppNotification(NotificationKind.Info, _text["notify.missed"], _text.Format("notify.missed_text", missed)));
        }

        if (changed)
        {
            AttentionChanged?.Invoke(this, EventArgs.Empty);
        }

        await ArmAsync(cancellationToken, stuck ? SchedulerLimits.ClaimRetry : TimeSpan.Zero).ConfigureAwait(false);
    }

    private async Task ArmAsync(CancellationToken cancellationToken, TimeSpan atLeast = default)
    {
        var next = _stopped ? null : await _posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false);
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
            NextPostUtc = next?.ScheduledAtUtc;
            if (_stopped || _paused)
            {
                return;
            }

            var wait = next?.ScheduledAtUtc is { } at ? at - _clock.GetUtcNow() : SchedulerLimits.SafetyInterval;
            wait = wait < atLeast ? atLeast : wait > SchedulerLimits.SafetyInterval ? SchedulerLimits.SafetyInterval : wait;
            NextWakeUtc = _clock.GetUtcNow() + wait;
            _timer = _clock.CreateTimer(_ => _ = TickAsync(), null, wait, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task TickAsync(DateTimeOffset? lateFromUtc = null)
    {
        try
        {
            await RunDueAsync(lateFromUtc, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The schedule must keep going: the error is logged with its reason, shown, and the timer is set again.
            _logger.LogError(ex, "The scheduler could not handle the due posts");
            _notifier.Notify(new AppNotification(NotificationKind.Failed, _text["notify.scheduler_error"], ex.Message));
            try
            {
                await ArmAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception again)
            {
                _logger.LogError(again, "The scheduler could not set its timer again");
            }
        }
    }
}
