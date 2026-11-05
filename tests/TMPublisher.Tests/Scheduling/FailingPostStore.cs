using TMPublisher.Core.Posts;

namespace TMPublisher.Tests.Scheduling;

/// <summary>The real post store with database errors on demand (a full disk, a read-only file, a damaged row).</summary>
internal sealed class FailingPostStore(IPostStore inner) : IPostStore
{
    /// <summary>Reads of one post that fail before the real ones are made.</summary>
    public int FailGets { get; set; }

    /// <summary>Whether claims fail.</summary>
    public bool FailClaims { get; set; }

    /// <summary>Whether reading the next scheduled post fails.</summary>
    public bool FailNext { get; set; }

    public int Claims { get; private set; }

    public Task<Post?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (FailGets > 0)
        {
            FailGets--;
            throw new IOException("disk I/O error");
        }

        return inner.GetAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<Post>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        inner.ListAsync(fromUtc, toUtc, cancellationToken);

    public Task<IReadOnlyList<Post>> ListByStatusAsync(IReadOnlyCollection<PostStatus> statuses, CancellationToken cancellationToken) =>
        inner.ListByStatusAsync(statuses, cancellationToken);

    public Task<Post?> NextScheduledAsync(CancellationToken cancellationToken) =>
        FailNext ? throw new IOException("file is not a database") : inner.NextScheduledAsync(cancellationToken);

    public Task SaveAsync(Post post, CancellationToken cancellationToken) => inner.SaveAsync(post, cancellationToken);

    public Task<bool> SaveEditAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        inner.SaveEditAsync(post, expectedStates, cancellationToken);

    public Task<bool> SaveDecisionAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        inner.SaveDecisionAsync(post, expectedStates, cancellationToken);

    public Task<bool> TryMoveAsync(PostClaim claim, CancellationToken cancellationToken)
    {
        Claims++;
        return FailClaims ? throw new IOException("database or disk is full") : inner.TryMoveAsync(claim, cancellationToken);
    }

    public Task SaveTargetAsync(PostTarget target, CancellationToken cancellationToken) => inner.SaveTargetAsync(target, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => inner.DeleteAsync(id, cancellationToken);
}
