using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPostStore"/> on the encrypted database.</summary>
public sealed class PostStore(IDbContextFactory<PublisherDbContext> factory, TimeProvider clock) : IPostStore
{
    /// <inheritdoc />
    public async Task<Post?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return Ordered(await db.Posts.AsNoTracking().Include(p => p.Targets).FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var posts = await db.Posts.AsNoTracking().Include(p => p.Targets)
            .Where(p => (p.ScheduledAtUtc != null && p.ScheduledAtUtc >= fromUtc && p.ScheduledAtUtc < toUtc)
                || (p.ScheduledAtUtc == null && p.CreatedAtUtc >= fromUtc && p.CreatedAtUtc < toUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return posts.Select(p => Ordered(p)!).OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListByStatusAsync(IReadOnlyCollection<PostStatus> statuses, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var posts = await db.Posts.AsNoTracking().Include(p => p.Targets).Where(p => statuses.Contains(p.Status))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return posts.Select(p => Ordered(p)!).OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<Post?> NextScheduledAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return Ordered(await db.Posts.AsNoTracking().Include(p => p.Targets)
            .Where(p => p.Status == PostStatus.Scheduled && p.ScheduledAtUtc != null)
            .OrderBy(p => p.ScheduledAtUtc).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task SaveAsync(Post post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        var now = clock.GetUtcNow();
        if (post.CreatedAtUtc == default)
        {
            post.CreatedAtUtc = now;
        }

        post.UpdatedAtUtc = now;
        foreach (var target in post.Targets)
        {
            target.PostId = post.Id;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await WriteAsync(db, post, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> SaveEditAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(expectedStates);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // SQLite takes the write lock at BEGIN (immediate), so the scheduler cannot change the post between this check and the write.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var stored = await db.Posts.AsNoTracking().Include(p => p.Targets).FirstOrDefaultAsync(p => p.Id == post.Id, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            if (!expectedStates.Contains(stored.Status))
            {
                return false;
            }

            post.Targets = PostEditing.KeepFinished(post.Targets, stored.Targets.OrderBy(t => t.Order).ToList());
            post.CreatedAtUtc = stored.CreatedAtUtc;
        }

        post.UpdatedAtUtc = clock.GetUtcNow();
        if (post.CreatedAtUtc == default)
        {
            post.CreatedAtUtc = post.UpdatedAtUtc;
        }

        foreach (var target in post.Targets)
        {
            target.PostId = post.Id;
        }

        await WriteAsync(db, post, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task SaveTargetAsync(PostTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Targets.Update(target);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Targets.Where(t => t.PostId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Posts.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteAsync(PublisherDbContext db, Post post, CancellationToken cancellationToken)
    {
        await db.Targets.Where(t => t.PostId == post.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Posts.AnyAsync(p => p.Id == post.Id, cancellationToken).ConfigureAwait(false);
        db.Entry(post).State = exists ? EntityState.Modified : EntityState.Added;
        foreach (var target in post.Targets)
        {
            db.Entry(target).State = EntityState.Added;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Post? Ordered(Post? post)
    {
        if (post is not null)
        {
            post.Targets = post.Targets.OrderBy(t => t.Order).ToList();
        }

        return post;
    }
}
