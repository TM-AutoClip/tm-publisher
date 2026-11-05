using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Scheduling.Accounts;

/// <summary>
/// Fresh credentials for an account: refreshes with the user's own app when they are about to expire, and once more when the platform
/// rejects the token; every refresh is stored at once (a rotated refresh token must not be lost). A rejected refresh marks the account
/// "connect again".
/// </summary>
public sealed class CredentialRenewer(IPlatformDirectory platforms, ICredentialStore credentials, IAccountStore accounts, TimeProvider clock,
    ILogger<CredentialRenewer> logger)
{
    /// <summary>The account's credentials, refreshed first when needed.</summary>
    public async Task<Credentials> GetFreshAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var stored = await credentials.GetAsync(account.Id, cancellationToken).ConfigureAwait(false);
        var refresher = platforms.Get(account.Platform).Refresher;
        return refresher is not null && refresher.NeedsRefresh(stored, clock.GetUtcNow())
            ? await RefreshAsync(account, stored, refresher, cancellationToken).ConfigureAwait(false)
            : stored;
    }

    /// <summary>The "renew once after a 401" function for a publish request; <c>null</c> when the platform cannot refresh.</summary>
    public Func<CancellationToken, Task<Credentials>>? RenewFor(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var refresher = platforms.Get(account.Platform).Refresher;
        return refresher is null
            ? null
            : async ct => await RefreshAsync(account, await credentials.GetAsync(account.Id, ct).ConfigureAwait(false), refresher, ct).ConfigureAwait(false);
    }

    /// <summary>Stores credentials returned by a test (they may have been refreshed).</summary>
    public Task SaveAsync(Guid accountId, Credentials values, CancellationToken cancellationToken) =>
        credentials.SaveAsync(accountId, values, cancellationToken);

    /// <summary>Marks the account "connect again".</summary>
    public async Task MarkReconnectAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.State != AccountState.NeedsReconnect)
        {
            account.State = AccountState.NeedsReconnect;
            await accounts.SaveAsync(account, cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Account {AccountId} ({Platform}) needs to be connected again", account.Id, account.Platform);
        }
    }

    private async Task<Credentials> RefreshAsync(Account account, Credentials stored, ITokenRefresher refresher, CancellationToken cancellationToken)
    {
        try
        {
            var fresh = await refresher.RefreshAsync(stored, cancellationToken).ConfigureAwait(false);
            await credentials.SaveAsync(account.Id, fresh, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Tokens of account {AccountId} ({Platform}) refreshed", account.Id, account.Platform);
            return fresh;
        }
        catch (PlatformAuthException)
        {
            await MarkReconnectAsync(account, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
