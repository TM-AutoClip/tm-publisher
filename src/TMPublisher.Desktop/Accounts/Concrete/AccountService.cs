using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Scheduling.Accounts;

namespace TMPublisher.Desktop.Accounts;

/// <summary>Stores connected accounts and runs the connection test of a stored account.</summary>
public sealed class AccountService(IAccountStore accounts, ICredentialStore credentials, IPlatformDirectory platforms, CredentialRenewer renewer,
    TimeProvider clock, ILogger<AccountService> logger)
{
    /// <summary>
    /// Saves a finished connection: the same platform account (same external id) is updated in place, otherwise a new account is added.
    /// </summary>
    public async Task<Account> SaveAsync(string platform, ConnectionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        var all = await accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        var account = all.FirstOrDefault(a => a.Platform == platform && a.ExternalId == result.Identity.ExternalId) ?? new Account
        {
            Platform = platform,
            ConnectedAtUtc = clock.GetUtcNow(),
        };
        account.DisplayName = result.Identity.DisplayName;
        account.Handle = result.Identity.Handle;
        account.ExternalId = result.Identity.ExternalId;
        account.State = AccountState.Active;
        account.ReconnectByUtc = result.ReconnectByUtc;
        await accounts.SaveAsync(account, cancellationToken).ConfigureAwait(false);
        await credentials.SaveAsync(account.Id, result.Credentials, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Account {AccountId} ({Platform}) connected", account.Id, platform);
        return account;
    }

    /// <summary>"Test connection" of a stored account: refreshes when needed, reads the identity; marks "connect again" on a rejected token.</summary>
    public async Task<AccountIdentity> TestAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        try
        {
            var fresh = await renewer.GetFreshAsync(account, cancellationToken).ConfigureAwait(false);
            var (identity, tested) = await platforms.Get(account.Platform).Connection.TestAsync(fresh, cancellationToken).ConfigureAwait(false);
            if (!ReferenceEquals(tested, fresh))
            {
                await renewer.SaveAsync(account.Id, tested, cancellationToken).ConfigureAwait(false);
            }

            if (account.State != AccountState.Active)
            {
                account.State = AccountState.Active;
                await accounts.SaveAsync(account, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation("Connection test of account {AccountId} ({Platform}) passed", account.Id, account.Platform);
            return identity;
        }
        catch (PlatformAuthException ex)
        {
            logger.LogWarning(ex, "Connection test of account {AccountId} ({Platform}) failed: token rejected", account.Id, account.Platform);
            await renewer.MarkReconnectAsync(account, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Removes the account, its secrets and its group memberships.</summary>
    public Task DeleteAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        logger.LogInformation("Account {AccountId} ({Platform}) removed", account.Id, account.Platform);
        return accounts.DeleteAsync(account.Id, cancellationToken);
    }
}
