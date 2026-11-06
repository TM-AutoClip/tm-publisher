using Microsoft.Extensions.Logging;
using TMPublisher.Core.Instance;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Security;

namespace TMPublisher.Scheduling;

/// <summary>
/// One timer for the whole schedule (it runs in the background scheduler process, the only place posts are sent from). The timer is set to the
/// earliest scheduled post, at most <see cref="SchedulerLimits.SafetyInterval"/> ahead, and set again whenever something changes. No loop, no
/// polling in between. A due post is first claimed in the database (scheduled → publishing, only if nobody changed it meanwhile) and then read
/// again and sent, so an edit, a move or a second scheduler can never make it go out twice or with old content. Posts are sent one at a time.
/// A post found more than five minutes late (the computer was off or asleep, scheduling was paused) is not sent on its own: it becomes
/// "missed" and waits for the user. Nothing is claimed while an old TM Publisher (0.1.0–0.1.2, which sends without claiming) runs on the
/// same data, and nothing new is claimed once a stop was asked for.
/// </summary>
public sealed class PostScheduler : IDisposable
{
    private readonly IPostStore _posts;
    private readonly PostSender _sender;
    private readonly IOldVersionCheck _oldVersion;
    private readonly ProcessingWatcher _watcher;
    private readonly INotifier _notifier;
    private readonly ILocalizer _text;
    private readonly TimeProvider _clock;
    private readonly ILogger<PostScheduler> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _timerGate = new();
    private ITimer? _timer;
    private bool _started;
    private volatile bool _stopped;
    private volatile bool _paused;
    private int _stopRequests;
    private int _failures;
    private int _oldVersionSeen;
    private DateTimeOffset? _loggedNext;
    private bool _nextLogged;
    private DateTimeOffset _heartbeatUtc = DateTimeOffset.MinValue;

    /// <summary>Creates.</summary>
    public PostScheduler(IPostStore posts, PostSender sender, IOldVersionCheck oldVersion, ProcessingWatcher watcher, INotifier notifier,
        ILocalizer text, TimeProvider clock, ILogger<PostScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _posts = posts;
        _sender = sender;
        _oldVersion = oldVersion;
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

    private bool StopRequested => Volatile.Read(ref _stopRequests) > 0;

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
    /// Stops for good (the scheduler process is quitting): the running pass claims no further post, the post being sent is finished, then no
    /// timer fires again. <paramref name="onlyIfIdle"/>: returns <c>false</c> without stopping when a pass still holds the schedule after
    /// <see cref="SchedulerLimits.IdleStopWait"/> (a post is being sent); a pass that is only reading or claiming sees the request and ends first.
    /// The check and taking the schedule are one step, so a post claimed right after the check can never be cut off by this stop.
    /// </summary>
    public async Task<bool> StopAsync(bool onlyIfIdle, CancellationToken cancellationToken)
    {
        if (_stopped)
        {
            return true;
        }

        Interlocked.Increment(ref _stopRequests);
        try
        {
            if (onlyIfIdle)
            {
                if (!await _gate.WaitAsync(SchedulerLimits.IdleStopWait, cancellationToken).ConfigureAwait(false))
                {
                    // Sending: the request is taken back, the pass goes on with the next due post.
                    Interlocked.Decrement(ref _stopRequests);
                    _logger.LogInformation("Not stopping: a post is being sent");
                    return false;
                }
            }
            else
            {
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _stopRequests);
            throw;
        }

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
            await StopForUserAsync(post, _text["problem.interrupted"], cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Post {PostId} was interrupted when the scheduler ended; waiting for the user", post.Id);
        }

        await _watcher.ResumeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A claimed post whose sending stopped half way (the scheduler ended, or an error escaped the sender): the targets being sent become
    /// failed with <paramref name="reason"/> and the post "stopped", which the user sees under Attention and can retry, skip or cancel. Finished
    /// targets keep their state, so a retry sends only what is left.
    /// </summary>
    private async Task StopForUserAsync(Post post, string reason, CancellationToken cancellationToken)
    {
        foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Publishing))
        {
            target.Status = TargetStatus.Failed;
            target.Message = reason;
        }

        post.Status = PostStatus.Stopped;
        await _posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>An exception escaped after the claim: the post must not stay "publishing" (nothing would send, list or retry it).</summary>
    private async Task StopAfterErrorAsync(Guid postId, Exception error)
    {
        var reason = Redactor.Redact(error.Message);
        try
        {
            if (await _posts.GetAsync(postId, CancellationToken.None).ConfigureAwait(false) is { Status: PostStatus.Publishing } stuck)
            {
                await StopForUserAsync(stuck, _text.Format("problem.send_error", reason), CancellationToken.None).ConfigureAwait(false);
                _logger.LogWarning("Post {PostId} was set to wait for the user after the error", postId);
            }
        }
        catch (Exception again)
        {
            _logger.LogError(again, "Post {PostId} stays publishing after the error; it waits for the user after the next start of the scheduler", postId);
        }

        _notifier.Notify(new AppNotification(NotificationKind.Failed, _text["notify.scheduler_error"], reason));
    }

    /// <summary>Whether an old TM Publisher runs on the same data; shown once each time it is found (the check repeats every minute).</summary>
    private bool OldVersionBlocks()
    {
        if (!_oldVersion.OldVersionRuns())
        {
            if (Interlocked.Exchange(ref _oldVersionSeen, 0) == 1)
            {
                _logger.LogInformation("The older TM Publisher has ended; posts are sent again");
            }

            return false;
        }

        if (Interlocked.Exchange(ref _oldVersionSeen, 1) == 0)
        {
            _logger.LogError("An older TM Publisher (0.1.0-0.1.2) runs on the same data and sends posts without claiming them; nothing is claimed "
                + "here until it ends (checked again every {Wait})", SchedulerLimits.ClaimRetry);
            _notifier.Notify(new AppNotification(NotificationKind.Failed, _text["notify.scheduler_error"], _text["scheduler.old_version"]));
        }

        return true;
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

            while (!_paused && !StopRequested
                && await _posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false) is { ScheduledAtUtc: { } at } post && at <= _clock.GetUtcNow())
            {
                if (!refused.Add(post.Id))
                {
                    // The claim of this post failed once in this run and it is still listed as due: never loop on it; the next run tries again.
                    _logger.LogError("Post {PostId} is due but could not be claimed twice in one run; trying again in {Wait}", post.Id, SchedulerLimits.ClaimRetry);
                    stuck = true;
                    break;
                }

                if (StopRequested)
                {
                    // Asked to stop while the schedule was read: no new claim (the post stays scheduled for the next start).
                    _logger.LogInformation("Stopping: post {PostId} is left scheduled", post.Id);
                    break;
                }

                if (OldVersionBlocks())
                {
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

                IsSending = true;
                try
                {
                    // Claimed: read again, so exactly what is stored now is sent.
                    if (await _posts.GetAsync(post.Id, cancellationToken).ConfigureAwait(false) is not { } claimed)
                    {
                        _logger.LogWarning("Post {PostId} was deleted right after it was claimed; nothing sent", post.Id);
                        continue;
                    }

                    await _sender.SendAsync(claimed, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    // A database or file error before, during or after the requests (the sender records platform errors itself).
                    _logger.LogError(ex, "Post {PostId} could not be sent after it was claimed", post.Id);
                    await StopAfterErrorAsync(post.Id, ex).ConfigureAwait(false);
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
            LogNext(next);
        }
    }

    /// <summary>
    /// One line when the next post changes (not at every wake-up), and a sign of life at most once per <see cref="SchedulerLimits.Heartbeat"/>
    /// (the timer wakes every <see cref="SchedulerLimits.SafetyInterval"/> anyway; no extra timer).
    /// </summary>
    private void LogNext(Post? next)
    {
        var now = _clock.GetUtcNow();
        if (!_nextLogged || _loggedNext != next?.ScheduledAtUtc)
        {
            _nextLogged = true;
            _loggedNext = next?.ScheduledAtUtc;
            _heartbeatUtc = now;
            if (next?.ScheduledAtUtc is { } at)
            {
                _logger.LogInformation("Next post {PostId} at {Time:yyyy-MM-dd HH:mm} UTC", next.Id, at);
            }
            else
            {
                _logger.LogInformation("No post is scheduled");
            }

            return;
        }

        if (now - _heartbeatUtc >= SchedulerLimits.Heartbeat)
        {
            _heartbeatUtc = now;
            _logger.LogInformation("Scheduler running; next post {Next}", _loggedNext is { } at ? at.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture) : "none");
        }
    }

    private async Task TickAsync(DateTimeOffset? lateFromUtc = null)
    {
        try
        {
            await RunDueAsync(lateFromUtc, CancellationToken.None).ConfigureAwait(false);
            if (Interlocked.Exchange(ref _failures, 0) > 0)
            {
                _logger.LogInformation("The scheduler handles the due posts again");
            }
        }
        catch (Exception ex)
        {
            // The schedule must keep going: the error is logged with its reason, shown once per run of errors, and the timer is set again
            // after a growing wait (1, 2, 4, 8, then 15 minutes), never at once: a full disk or a read-only database would fail again at once.
            var failures = Interlocked.Increment(ref _failures);
            var wait = Backoff(failures);
            _logger.LogError(ex, "The scheduler could not handle the due posts (error {Count} in a row); trying again in {Wait}", failures, wait);
            if (failures == 1)
            {
                _notifier.Notify(new AppNotification(NotificationKind.Failed, _text["notify.scheduler_error"], Redactor.Redact(ex.Message)));
            }

            try
            {
                await ArmAsync(CancellationToken.None, wait).ConfigureAwait(false);
            }
            catch (Exception again)
            {
                // The schedule could not even be read: a timer that does not need the database is set, so the scheduler never ends up idle.
                _logger.LogError(again, "The scheduler could not read the schedule to set its timer; trying again in {Wait}", wait);
                ArmBlind(wait);
            }
        }
    }

    /// <summary>The wait after the <paramref name="failures"/>-th error in a row.</summary>
    internal static TimeSpan Backoff(int failures)
    {
        var ticks = SchedulerLimits.ClaimRetry.Ticks << Math.Clamp(failures - 1, 0, 4);
        return TimeSpan.FromTicks(Math.Min(ticks, SchedulerLimits.SafetyInterval.Ticks));
    }

    private void ArmBlind(TimeSpan wait)
    {
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
            if (_stopped || _paused)
            {
                return;
            }

            NextWakeUtc = _clock.GetUtcNow() + wait;
            _timer = _clock.CreateTimer(_ => _ = TickAsync(), null, wait, Timeout.InfiniteTimeSpan);
        }
    }
}
