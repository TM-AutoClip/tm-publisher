namespace TMPublisher.Core.Posts;

/// <summary>
/// The single scheduler: sends due posts, and carries out the user's decisions on posts that wait for one (missed, stopped, blocked).
/// </summary>
public interface IPostScheduler
{
    /// <summary>Raised when the posts waiting for a decision change.</summary>
    event EventHandler? AttentionChanged;

    /// <summary>Re-reads the schedule (a post was saved, the computer woke up, the clock changed). No effect before the scheduler starts.</summary>
    void Wake();

    /// <summary>Missed / blocked post: send it now.</summary>
    Task SendNowAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Missed / blocked post: schedule it again.</summary>
    Task RescheduleAsync(Guid postId, DateTimeOffset atUtc, CancellationToken cancellationToken);

    /// <summary>Cancels what is not published yet (published targets stay published).</summary>
    Task CancelAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Stopped post: tries the failed target again, then continues.</summary>
    Task RetryAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Stopped post: skips the failed target and continues with the rest.</summary>
    Task SkipFailedAsync(Guid postId, CancellationToken cancellationToken);
}
