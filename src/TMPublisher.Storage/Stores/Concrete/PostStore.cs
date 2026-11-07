using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPostStore"/> on the encrypted database (the window and a run of the system scheduler share the file).</summary>
public sealed class PostStore(IDbContextFactory<PublisherDbContext> contexts, TimeProvider clock) : StoreBase(contexts), IPostStore
{
    /// <inheritdoc />
    public async Task<Post?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = Open();
        return await WithTargets(context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        await using var context = Open();
        var posts = await WithTargets(context)
            .Where(p => (p.ScheduledAtUtc != null && p.ScheduledAtUtc >= fromUtc && p.ScheduledAtUtc < toUtc)
                || (p.ScheduledAtUtc == null && p.CreatedAtUtc >= fromUtc && p.CreatedAtUtc < toUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return posts.OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListByStatusAsync(IReadOnlyCollection<PostStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        if (statuses.Count == 0)
        {
            return [];
        }

        var wanted = statuses.ToList();
        await using var context = Open();
        var posts = await WithTargets(context).Where(p => wanted.Contains(p.Status)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return posts.OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<Post?> NextScheduledAsync(CancellationToken cancellationToken)
    {
        await using var context = Open();
        return await WithTargets(context).Where(p => p.Status == PostStatus.Scheduled && p.ScheduledAtUtc != null).OrderBy(p => p.ScheduledAtUtc)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SaveAsync(Post post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        var now = clock.GetUtcNow();
        if (post.CreatedAtUtc == default)
        {
            post.CreatedAtUtc = now;
        }

        post.UpdatedAtUtc = now;
        return InWriteTransactionAsync(context => WriteAsync(context, post, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SaveEditAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        SaveIfAsync(post, expectedStates, keepFinished: true, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SaveDecisionAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        SaveIfAsync(post, expectedStates, keepFinished: false, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryMoveAsync(PostClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var now = clock.GetUtcNow();
        await using var context = Open();
        var claimed = context.Posts.Where(p => p.Id == claim.PostId && p.Status == claim.From);
        claimed = claim.ScheduledAtUtc is { } at ? claimed.Where(p => p.ScheduledAtUtc == at) : claimed.Where(p => p.ScheduledAtUtc == null);
        var changed = await claimed.ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, claim.To).SetProperty(p => p.UpdatedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        return changed == 1;
    }

    /// <inheritdoc />
    public async Task SaveTargetAsync(PostTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        await using var context = Open();
        context.Entry(target).State = EntityState.Modified;
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // No row was changed: the post was deleted (or edited without this target) meanwhile.
            throw new InvalidOperationException($"Target {target.Id} of post {target.PostId} is no longer stored; its state was not saved.", ex);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        InWriteTransactionAsync(async context =>
        {
            await context.Targets.Where(t => t.PostId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.Posts.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Posts read without tracking, each with its targets in sending order.</summary>
    private static IQueryable<Post> WithTargets(PublisherDbContext context) => context.Posts.AsNoTracking().Include(p => p.Targets.OrderBy(t => t.Order));

    /// <summary>
    /// Writes the post only while the stored one is in <paramref name="expectedStates"/>, in one write transaction (the scheduler, also in the
    /// other process, cannot change the post between this check and the write). An edit keeps the stored targets that already reached their
    /// platform (<see cref="PostEditing.KeepFinished"/>); a decision is stored as given.
    /// </summary>
    private Task<bool> SaveIfAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, bool keepFinished, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(expectedStates);
        return InWriteTransactionAsync(async context =>
        {
            var stored = await WithTargets(context).FirstOrDefaultAsync(p => p.Id == post.Id, cancellationToken).ConfigureAwait(false);
            if (stored is not null)
            {
                if (!expectedStates.Contains(stored.Status))
                {
                    return false;
                }

                if (keepFinished)
                {
                    post.Targets = PostEditing.KeepFinished(post.Targets, stored.Targets);
                }

                post.CreatedAtUtc = stored.CreatedAtUtc;
            }

            post.UpdatedAtUtc = clock.GetUtcNow();
            if (post.CreatedAtUtc == default)
            {
                post.CreatedAtUtc = post.UpdatedAtUtc;
            }

            await WriteAsync(context, post, cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Inside a write transaction: replaces the post row (inserted or updated, so the targets are not cascaded away) and all its targets (the
    /// stored ones are deleted, the given ones inserted).
    /// </summary>
    private static async Task WriteAsync(PublisherDbContext context, Post post, CancellationToken cancellationToken)
    {
        foreach (var target in post.Targets)
        {
            target.PostId = post.Id;
        }

        await context.Targets.Where(t => t.PostId == post.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await TrackAsync(context, post, cancellationToken).ConfigureAwait(false);
        foreach (var target in post.Targets)
        {
            context.Entry(target).State = EntityState.Added;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
