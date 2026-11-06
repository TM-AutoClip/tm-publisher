namespace TMPublisher.Core.Posts;

/// <summary>
/// The schedule as the window sees it: while the window is open it sends the posts itself; it stores the user's decisions on posts that wait
/// for one (missed, stopped, blocked), reads the schedule again after a change and sets the system scheduler's next start for the time it is
/// closed.
/// </summary>
public interface IPostScheduler
{
    /// <summary>Raised when the posts waiting for a decision may have changed (also when a post was sent or stopped).</summary>
    event EventHandler? AttentionChanged;

    /// <summary>Reads the schedule again (a post was saved or moved, a setting changed). Never waits for sending.</summary>
    void Wake();

    /// <summary>
    /// The database was replaced (restore): the scheduler forgets its timer and follow-ups and starts again on the new data (posts left
    /// "publishing" wait for the user, processing follow-ups resume, due posts are handled, the timer is set).
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken);

    /// <summary>Missed / blocked post: send it now. <c>false</c> when the post changed meanwhile and nothing was decided.</summary>
    Task<bool> SendNowAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Missed / blocked post: schedule it again. <c>false</c> when the post changed meanwhile.</summary>
    Task<bool> RescheduleAsync(Guid postId, DateTimeOffset atUtc, CancellationToken cancellationToken);

    /// <summary>Cancels what is not published yet (published targets stay published). <c>false</c> when the post changed meanwhile.</summary>
    Task<bool> CancelAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Stopped post: tries the failed target again, then continues. <c>false</c> when the post changed meanwhile.</summary>
    Task<bool> RetryAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>Stopped post: skips the failed target and continues with the rest. <c>false</c> when the post changed meanwhile.</summary>
    Task<bool> SkipFailedAsync(Guid postId, CancellationToken cancellationToken);
}
