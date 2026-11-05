using Microsoft.Extensions.Logging;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;

namespace TMPublisher.Scheduling;

/// <summary>
/// One timer for the whole schedule: it is set to the earliest scheduled post (at most 24 hours ahead) and set again whenever something
/// changes. No loop, no polling. Posts are sent one at a time. A post found more than five minutes late (the computer was off or asleep)
/// is not sent on its own: it becomes "missed" and waits for the user (send now / new time / cancel).
/// </summary>
public sealed class PostScheduler : IPostScheduler, IDisposable
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

    /// <inheritdoc />
    public event EventHandler? AttentionChanged;

    /// <summary>When the timer is set to fire next (UTC); <c>null</c> when nothing is scheduled.</summary>
    public DateTimeOffset? NextWakeUtc { get; private set; }

    /// <summary>
    /// Starts: posts left "publishing" by a closed program become "stopped" (the finished targets are known from the local record), the
    /// processing follow-ups resume, due posts are handled and the timer is set.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var post in await _posts.ListByStatusAsync([PostStatus.Publishing], cancellationToken).ConfigureAwait(false))
        {
            foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Publishing))
            {
                target.Status = TargetStatus.Failed;
                target.Message = _text["problem.interrupted"];
            }

            post.Status = PostStatus.Stopped;
            await _posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Post {PostId} was interrupted when the program closed; waiting for the user", post.Id);
        }

        await _watcher.ResumeAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
        await RunDueAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Wake()
    {
        if (_started)
        {
            _ = TickAsync();
        }
    }

    /// <summary>Handles everything that is due now and sets the timer (also what the timer runs).</summary>
    public async Task RunDueAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var missed = 0;
        var changed = false;

        // Lateness is measured from the moment this run started: posts that fall due while an earlier post is uploading are sent, not "missed".
        var runStart = _clock.GetUtcNow();
        try
        {
            while (await _posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false) is { ScheduledAtUtc: { } at } post && at <= _clock.GetUtcNow())
            {
                if (runStart - at > SchedulerLimits.LateTolerance)
                {
                    post.Status = PostStatus.Missed;
                    post.UpdatedAtUtc = _clock.GetUtcNow();
                    await _posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Post {PostId} missed its time ({Late} late); waiting for the user", post.Id, runStart - at);
                    missed++;
                    changed = true;
                    continue;
                }

                var state = await _sender.SendAsync(post, cancellationToken).ConfigureAwait(false);
                changed |= state is PostStatus.Blocked or PostStatus.Stopped;
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

        await ArmAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SendNowAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, post =>
    {
        ResetUnsent(post);
        post.Status = PostStatus.Scheduled;
        post.ScheduledAtUtc = _clock.GetUtcNow();
    }, cancellationToken);

    /// <inheritdoc />
    public Task RescheduleAsync(Guid postId, DateTimeOffset atUtc, CancellationToken cancellationToken) => DecideAsync(postId, post =>
    {
        ResetUnsent(post);
        post.Status = PostStatus.Scheduled;
        post.ScheduledAtUtc = atUtc;
    }, cancellationToken);

    /// <inheritdoc />
    public Task CancelAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, post =>
    {
        foreach (var target in post.Targets.Where(t => t.Status is TargetStatus.Pending or TargetStatus.Failed))
        {
            target.Status = TargetStatus.Canceled;
        }

        post.Status = post.Targets.Any(PostEditing.IsSent) ? PostStatus.Done : PostStatus.Canceled;
    }, cancellationToken);

    /// <inheritdoc />
    public Task RetryAsync(Guid postId, CancellationToken cancellationToken) => SendNowAsync(postId, cancellationToken);

    /// <inheritdoc />
    public Task SkipFailedAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, post =>
    {
        foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Failed))
        {
            target.Status = TargetStatus.Skipped;
        }

        if (post.Targets.Any(t => t.Status == TargetStatus.Pending))
        {
            post.Status = PostStatus.Scheduled;
            post.ScheduledAtUtc = _clock.GetUtcNow();
        }
        else
        {
            post.Status = PostStatus.Done;
        }
    }, cancellationToken);

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

    private static void ResetUnsent(Post post)
    {
        foreach (var target in post.Targets.Where(t => t.Status is TargetStatus.Failed or TargetStatus.Publishing))
        {
            target.Status = TargetStatus.Pending;
            target.Message = null;
        }

        foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Pending))
        {
            target.Message = null;
        }
    }

    private async Task DecideAsync(Guid postId, Action<Post> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _posts.GetAsync(postId, cancellationToken).ConfigureAwait(false) is not { } post)
            {
                return;
            }

            change(post);
            post.UpdatedAtUtc = _clock.GetUtcNow();
            await _posts.SaveAsync(post, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Post {PostId}: user decision applied, now {Status}", post.Id, post.Status);
        }
        finally
        {
            _gate.Release();
        }

        AttentionChanged?.Invoke(this, EventArgs.Empty);
        if (_started)
        {
            await RunDueAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ArmAsync(CancellationToken cancellationToken)
    {
        var next = await _posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false);
        lock (_timerGate)
        {
            _timer?.Dispose();
            _timer = null;
            NextWakeUtc = null;
            if (next?.ScheduledAtUtc is not { } at)
            {
                return;
            }

            var wait = at - _clock.GetUtcNow();
            wait = wait < TimeSpan.Zero ? TimeSpan.Zero : wait > SchedulerLimits.LongestWait ? SchedulerLimits.LongestWait : wait;
            NextWakeUtc = _clock.GetUtcNow() + wait;
            _timer = _clock.CreateTimer(_ => _ = TickAsync(), null, wait, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task TickAsync()
    {
        try
        {
            await RunDueAsync(CancellationToken.None).ConfigureAwait(false);
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
