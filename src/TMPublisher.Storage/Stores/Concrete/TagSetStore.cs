using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ITagSetStore"/> on the encrypted database.</summary>
public sealed class TagSetStore(IDbContextFactory<PublisherDbContext> factory) : ITagSetStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TagSet>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.TagSets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(TagSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.TagSets.AnyAsync(t => t.Platform == set.Platform, cancellationToken).ConfigureAwait(false);
        db.Entry(set).State = exists ? EntityState.Modified : EntityState.Added;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
