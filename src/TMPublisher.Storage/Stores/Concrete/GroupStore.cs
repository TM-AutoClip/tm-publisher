using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IGroupStore"/> on the encrypted database.</summary>
public sealed class GroupStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), IGroupStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = Open();
        var groups = await context.Groups.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return groups.OrderBy(g => g.Name, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        return SaveRowAsync(group, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = Open();
        await context.Groups.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
