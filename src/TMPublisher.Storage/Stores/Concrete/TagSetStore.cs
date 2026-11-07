using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ITagSetStore"/> on the encrypted database.</summary>
public sealed class TagSetStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), ITagSetStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TagSet>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = Open();
        return await context.TagSets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SaveAsync(TagSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        return SaveRowAsync(set, cancellationToken);
    }
}
