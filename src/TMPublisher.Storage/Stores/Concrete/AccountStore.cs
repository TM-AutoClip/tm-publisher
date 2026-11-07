using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IAccountStore"/> on the encrypted database.</summary>
public sealed class AccountStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), IAccountStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = Open();
        var accounts = await context.Accounts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return accounts.OrderBy(a => a.Platform, StringComparer.Ordinal).ThenBy(a => a.DisplayName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public async Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = Open();
        return await context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SaveAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        return SaveRowAsync(account, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStateAsync(Guid id, AccountState state, CancellationToken cancellationToken)
    {
        await using var context = Open();
        await context.Accounts.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.State, state), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        InWriteTransactionAsync(async context =>
        {
            await context.Accounts.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.Credentials.Where(c => c.AccountId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            // The account leaves every group it was in (the JSON column's comparer sees the removed id; other groups are not written).
            foreach (var group in await context.Groups.ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                group.AccountIds.Remove(id);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
}
