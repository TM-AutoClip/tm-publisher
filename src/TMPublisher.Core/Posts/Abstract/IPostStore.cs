namespace TMPublisher.Core.Posts;

/// <summary>Posts with their targets.</summary>
public interface IPostStore
{
    /// <summary>One post with targets; <c>null</c> when missing.</summary>
    Task<Post?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Posts whose time is between <paramref name="fromUtc"/> and <paramref name="toUtc"/> (scheduled or sent), with targets.</summary>
    Task<IReadOnlyList<Post>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>Posts in the given states, with targets.</summary>
    Task<IReadOnlyList<Post>> ListByStatusAsync(IReadOnlyCollection<PostStatus> statuses, CancellationToken cancellationToken);

    /// <summary>The scheduled post with the earliest time; <c>null</c> when none.</summary>
    Task<Post?> NextScheduledAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the post and its targets.</summary>
    Task SaveAsync(Post post, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a change the user made to a stored post, in one transaction: refused (<c>false</c>) when the stored post is no longer in one of
    /// <paramref name="expectedStates"/> (the scheduler sent it meanwhile); targets already sent, skipped or canceled are kept as stored
    /// (<see cref="PostEditing.KeepFinished"/>). A post that is not stored yet is inserted.
    /// </summary>
    Task<bool> SaveEditAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken);

    /// <summary>Updates one target (status, ids, message).</summary>
    Task SaveTargetAsync(PostTarget target, CancellationToken cancellationToken);

    /// <summary>Deletes the post and its targets.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
