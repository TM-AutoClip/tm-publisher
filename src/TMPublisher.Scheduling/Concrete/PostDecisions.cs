using Microsoft.Extensions.Logging;
using TMPublisher.Core.Posts;

namespace TMPublisher.Scheduling;

/// <summary>
/// The user's decisions on posts that wait for one (missed, blocked, stopped): send now, new time, cancel, try again, skip the failed target.
/// A decision only changes the stored post, and only while it is still in a state the decision fits (one database transaction): nothing is sent
/// here. "Send now" schedules the post for now; the sender then claims and sends it like every other post, so there is one
/// sending path and a post cannot go out twice.
/// </summary>
public sealed class PostDecisions(IPostStore posts, TimeProvider clock, ILogger<PostDecisions> logger)
{
    /// <summary>States of a post that waits for the user.</summary>
    public static IReadOnlyCollection<PostStatus> Waiting { get; } = [PostStatus.Missed, PostStatus.Blocked, PostStatus.Stopped];

    private static readonly PostStatus[] Cancelable = [PostStatus.Missed, PostStatus.Blocked, PostStatus.Stopped, PostStatus.Scheduled, PostStatus.Draft];

    private static readonly PostStatus[] StoppedOnly = [PostStatus.Stopped];

    /// <summary>Missed / blocked / stopped post: send it now. <c>false</c> when the post changed meanwhile.</summary>
    public Task<bool> SendNowAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, Waiting, "send now", post =>
    {
        ResetUnsent(post);
        post.Status = PostStatus.Scheduled;
        post.ScheduledAtUtc = clock.GetUtcNow();
    }, cancellationToken);

    /// <summary>Missed / blocked / stopped post: schedule it again.</summary>
    public Task<bool> RescheduleAsync(Guid postId, DateTimeOffset atUtc, CancellationToken cancellationToken) => DecideAsync(postId, Waiting, "new time", post =>
    {
        ResetUnsent(post);
        post.Status = PostStatus.Scheduled;
        post.ScheduledAtUtc = atUtc;
    }, cancellationToken);

    /// <summary>Cancels what is not published yet (published targets stay published).</summary>
    public Task<bool> CancelAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, Cancelable, "cancel", post =>
    {
        foreach (var target in post.Targets.Where(t => t.Status is TargetStatus.Pending or TargetStatus.Failed))
        {
            target.Status = TargetStatus.Canceled;
        }

        post.Status = post.Targets.Any(PostEditing.IsSent) ? PostStatus.Done : PostStatus.Canceled;
    }, cancellationToken);

    /// <summary>Stopped post: tries the failed target again, then continues.</summary>
    public Task<bool> RetryAsync(Guid postId, CancellationToken cancellationToken) => SendNowAsync(postId, cancellationToken);

    /// <summary>Stopped post: skips the failed target and continues with the rest (now).</summary>
    public Task<bool> SkipFailedAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(postId, StoppedOnly, "skip failed", post =>
    {
        foreach (var target in post.Targets.Where(t => t.Status == TargetStatus.Failed))
        {
            target.Status = TargetStatus.Skipped;
        }

        if (post.Targets.Any(t => t.Status == TargetStatus.Pending))
        {
            post.Status = PostStatus.Scheduled;
            post.ScheduledAtUtc = clock.GetUtcNow();
        }
        else
        {
            post.Status = PostStatus.Done;
        }
    }, cancellationToken);

    private static void ResetUnsent(Post post)
    {
        foreach (var target in post.Targets.Where(t => t.Status is TargetStatus.Failed or TargetStatus.Publishing or TargetStatus.Pending))
        {
            target.Status = TargetStatus.Pending;
            target.Message = null;
        }
    }

    private async Task<bool> DecideAsync(Guid postId, IReadOnlyCollection<PostStatus> allowed, string decision, Action<Post> change, CancellationToken cancellationToken)
    {
        if (await posts.GetAsync(postId, cancellationToken).ConfigureAwait(false) is not { } post)
        {
            logger.LogWarning("Post {PostId}: decision '{Decision}' not applied, the post no longer exists", postId, decision);
            return false;
        }

        if (!allowed.Contains(post.Status))
        {
            logger.LogWarning("Post {PostId}: decision '{Decision}' not applied, the post is {Status} now", postId, decision, post.Status);
            return false;
        }

        change(post);
        if (!await posts.SaveDecisionAsync(post, allowed, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Post {PostId}: decision '{Decision}' not applied, the post changed while it was being decided", postId, decision);
            return false;
        }

        logger.LogInformation("Post {PostId}: decision '{Decision}' applied, now {Status}", postId, decision, post.Status);
        return true;
    }
}
