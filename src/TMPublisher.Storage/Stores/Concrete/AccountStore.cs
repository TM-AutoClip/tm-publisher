using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IAccountStore"/> on the encrypted database.</summary>
public sealed class AccountStore(IDbContextFactory<PublisherDbContext> factory) : IAccountStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Accounts.AsNoTracking().OrderBy(a => a.Platform).ThenBy(a => a.DisplayName).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await db.Accounts.AnyAsync(a => a.Id == account.Id, cancellationToken).ConfigureAwait(false);
        db.Entry(account).State = exists ? EntityState.Modified : EntityState.Added;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Accounts.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Credentials.Where(c => c.AccountId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        foreach (var group in await db.Groups.ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (group.AccountIds.Remove(id))
            {
                db.Groups.Update(group);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
