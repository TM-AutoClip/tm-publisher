using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Localization;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Security;
using TMPublisher.Scheduling.Accounts;

namespace TMPublisher.Scheduling;

/// <summary>
/// Short follow-up of uploads the platform is still processing (YouTube, Facebook video): a few checks over about an hour, then the
/// target stays "processing" with a note. Each check is one request; nothing runs in between. While the window is open the checks are timers
/// (<see cref="Watch"/>); a run the system scheduler started does the checks whose time has come (<see cref="RunDueChecksAsync"/>) and tells
/// when the next one is due, counted from when the target was sent.
/// </summary>
public sealed class ProcessingWatcher(IPostStore posts, IAccountStore accounts, IPlatformDirectory platforms, CredentialRenewer renewer, INotifier notifier,
    ILocalizer text, TimeProvider clock, ILogger<ProcessingWatcher> logger) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, ITimer> _timers = [];

    /// <summary>Starts following a target (first check after the first interval).</summary>
    public void Watch(Guid postId, Guid targetId) => Arm(postId, targetId, 0);

    /// <summary>Follows every target still processing (after a restart).</summary>
    public async Task ResumeAsync(CancellationToken cancellationToken)
    {
        foreach (var post in await posts.ListByStatusAsync([PostStatus.Done, PostStatus.Stopped, PostStatus.Publishing], cancellationToken).ConfigureAwait(false))
        {
            foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Processing))
            {
                Watch(post.Id, target.Id);
            }
        }
    }

    /// <summary>
    /// For a run the system scheduler started (no timer lives past it): checks every target still processing whose check time has come, at
    /// the same intervals as the window's follow-up but counted from when it was sent (1, 3, 8, 18, 38 and 68 minutes); after the last one it
    /// stays "processing" with a note. Returns the time of the next check still to come; <c>null</c> when none.
    /// </summary>
    public async Task<DateTimeOffset?> RunDueChecksAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? next = null;
        foreach (var (post, target, points) in await ProcessingAsync(cancellationToken).ConfigureAwait(false))
        {
            var now = clock.GetUtcNow();
            if (now >= points[0])
            {
                try
                {
                    if (await CheckAsync(post.Id, target.Id, cancellationToken).ConfigureAwait(false) != TargetStatus.Processing)
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Post {PostId}: processing check of target {TargetId} failed ({Type}): {Reason}", post.Id, target.Id, ex.GetType().Name,
                        Redactor.Redact(ex.Message));
                }

                if (now >= points[^1])
                {
                    logger.LogInformation("Post {PostId}: target {TargetId} still processing after the last check", post.Id, target.Id);
                    await NoteStillProcessingAsync(post.Id, target.Id).ConfigureAwait(false);
                    continue;
                }
            }

            var due = points.First(p => p > now);
            next = next is { } earlier && earlier < due ? earlier : due;
        }

        return next;
    }

    /// <summary>When the next processing check is due, counted from when each target was sent; <c>null</c> when nothing is followed.</summary>
    public async Task<DateTimeOffset?> NextCheckUtcAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        DateTimeOffset? next = null;
        foreach (var (_, _, points) in await ProcessingAsync(cancellationToken).ConfigureAwait(false))
        {
            var due = points.FirstOrDefault(p => p > now, points[^1]);
            next = next is { } earlier && earlier < due ? earlier : due;
        }

        return next;
    }

    /// <summary>The check times of a target sent at <paramref name="sentUtc"/>.</summary>
    internal static IReadOnlyList<DateTimeOffset> CheckTimes(DateTimeOffset sentUtc)
    {
        var times = new List<DateTimeOffset>();
        var at = sentUtc;
        foreach (var wait in SchedulerLimits.ProcessingChecks)
        {
            at += wait;
            times.Add(at);
        }

        return times;
    }

    /// <summary>Checks once now (tests and the "check now" button).</summary>
    public async Task<TargetStatus?> CheckAsync(Guid postId, Guid targetId, CancellationToken cancellationToken)
    {
        var post = await posts.GetAsync(postId, cancellationToken).ConfigureAwait(false);
        var target = post?.Targets.FirstOrDefault(t => t.Id == targetId);
        if (post is null || target is null || target.Status != TargetStatus.Processing || target.ExternalId is not { Length: > 0 } id)
        {
            return null;
        }

        var account = await accounts.GetAsync(target.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null || platforms.Find(account.Platform) is not { } platform)
        {
            return null;
        }

        var who = platform.DisplayName + " · " + account.DisplayName;
        var fresh = await renewer.GetFreshAsync(account, cancellationToken).ConfigureAwait(false);
        var check = await platform.Publisher.CheckProcessingAsync(new PublishRequest(account, fresh, post, target, renewer.RenewFor(account, fresh)), id, cancellationToken)
            .ConfigureAwait(false);
        if (check.State == TargetStatus.Processing)
        {
            return check.State;
        }

        target.Status = check.State;
        target.Url = check.Url ?? target.Url;
        target.Message = check.Message is { } message ? Redactor.Redact(message) : null;
        await posts.SaveTargetAsync(target, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Post {PostId}: {Platform} target {TargetId} finished processing ({State})", post.Id, platform.Code, target.Id, check.State);
        notifier.Notify(check.State == TargetStatus.Failed
            ? new AppNotification(NotificationKind.Failed, text["notify.failed"], who + ": " + target.Message)
            : new AppNotification(NotificationKind.Published, text["notify.published"], who));
        return check.State;
    }

    /// <summary>Forgets every follow-up (the database was replaced).</summary>
    public void StopAll() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
        }
    }

    private void Arm(Guid postId, Guid targetId, int attempt)
    {
        lock (_gate)
        {
            if (_timers.Remove(targetId, out var old))
            {
                old.Dispose();
            }

            if (attempt >= SchedulerLimits.ProcessingChecks.Count)
            {
                logger.LogInformation("Post {PostId}: target {TargetId} still processing after the last check", postId, targetId);
                _ = NoteStillProcessingAsync(postId, targetId);
                return;
            }

            _timers[targetId] = clock.CreateTimer(_ => _ = RunAsync(postId, targetId, attempt), null, SchedulerLimits.ProcessingChecks[attempt], Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RunAsync(Guid postId, Guid targetId, int attempt)
    {
        try
        {
            if (await CheckAsync(postId, targetId, CancellationToken.None).ConfigureAwait(false) == TargetStatus.Processing)
            {
                Arm(postId, targetId, attempt + 1);
            }
            else
            {
                lock (_gate)
                {
                    _timers.Remove(targetId, out var timer);
                    timer?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            // A failed check (network, expired access) is logged and tried again at the next interval.
            logger.LogWarning("Post {PostId}: processing check of target {TargetId} failed ({Type}): {Reason}", postId, targetId, ex.GetType().Name, Redactor.Redact(ex.Message));
            Arm(postId, targetId, attempt + 1);
        }
    }

    /// <summary>The targets still followed (processing, not yet noted as still processing after the last check) with their check times.</summary>
    private async Task<List<(Post Post, PostTarget Target, IReadOnlyList<DateTimeOffset> Points)>> ProcessingAsync(CancellationToken cancellationToken)
    {
        var note = text["note.still_processing"];
        var found = new List<(Post, PostTarget, IReadOnlyList<DateTimeOffset>)>();
        foreach (var post in await posts.ListByStatusAsync([PostStatus.Done, PostStatus.Stopped, PostStatus.Publishing], cancellationToken).ConfigureAwait(false))
        {
            foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Processing && t.Message != note))
            {
                found.Add((post, target, CheckTimes(target.PublishedAtUtc ?? post.UpdatedAtUtc)));
            }
        }

        return found;
    }

    private async Task NoteStillProcessingAsync(Guid postId, Guid targetId)
    {
        try
        {
            var post = await posts.GetAsync(postId, CancellationToken.None).ConfigureAwait(false);
            if (post?.Targets.FirstOrDefault(t => t.Id == targetId) is { Status: TargetStatus.Processing } target)
            {
                target.Message = text["note.still_processing"];
                await posts.SaveTargetAsync(target, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Post {PostId}: could not store the processing note of target {TargetId}", postId, targetId);
        }
    }
}
