using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IGroupStore"/> on the encrypted database.</summary>
public sealed class GroupStore(IDbContextFactory<PublisherDbContext> factory) : IGroupStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Groups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Groups.AnyAsync(g => g.Id == group.Id, cancellationToken).ConfigureAwait(false);
        db.Entry(group).State = exists ? EntityState.Modified : EntityState.Added;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Groups.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
