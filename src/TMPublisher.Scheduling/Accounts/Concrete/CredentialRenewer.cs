using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Scheduling.Accounts;

/// <summary>
/// Fresh credentials for an account: refreshes with the user's own app when they are about to expire, and once more when the platform
/// rejects the token; every refresh is stored at once (a rotated refresh token must not be lost). A rejected refresh marks the account
/// "connect again". Refreshes of one account run one at a time, also across the background scheduler and the window
/// (<see cref="IRefreshGate"/>), and each re-reads the stored values first: with a rotating refresh token (X) a second refresh with the old
/// token would be refused and wrongly mark a working account.
/// </summary>
public sealed class CredentialRenewer(IPlatformDirectory platforms, ICredentialStore credentials, IAccountStore accounts, IRefreshGate gate, TimeProvider clock,
    ILogger<CredentialRenewer> logger)
{
    /// <summary>The account's credentials, refreshed first when needed.</summary>
    public async Task<Credentials> GetFreshAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var stored = await credentials.GetAsync(account.Id, cancellationToken).ConfigureAwait(false);
        var refresher = platforms.Get(account.Platform).Refresher;
        return refresher is not null && refresher.NeedsRefresh(stored, clock.GetUtcNow())
            ? await RefreshAsync(account, refresher, current => refresher.NeedsRefresh(current, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false)
            : stored;
    }

    /// <summary>
    /// The "renew once after a 401" function for a publish request made with <paramref name="used"/>; <c>null</c> when the platform cannot
    /// refresh. When another caller renewed the token meanwhile, its values are used instead of refreshing again.
    /// </summary>
    public Func<CancellationToken, Task<Credentials>>? RenewFor(Account account, Credentials used)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(used);
        var refresher = platforms.Get(account.Platform).Refresher;
        if (refresher is null)
        {
            return null;
        }

        var known = used;
        return async ct =>
        {
            known = await RefreshAsync(account, refresher, current => SameToken(current, known), ct).ConfigureAwait(false);
            return known;
        };
    }

    /// <summary>Stores credentials returned by a test (they may have been refreshed).</summary>
    public Task SaveAsync(Guid accountId, Credentials values, CancellationToken cancellationToken) =>
        credentials.SaveAsync(accountId, values, cancellationToken);

    /// <summary>Marks the account "connect again" (only its state is written; a rename in the window meanwhile is kept).</summary>
    public async Task MarkReconnectAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.State != AccountState.NeedsReconnect)
        {
            account.State = AccountState.NeedsReconnect;
            await accounts.SetStateAsync(account.Id, AccountState.NeedsReconnect, cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Account {AccountId} ({Platform}) needs to be connected again", account.Id, account.Platform);
        }
    }

    private static bool SameToken(Credentials a, Credentials b) =>
        string.Equals(a[CredentialKeys.AccessToken], b[CredentialKeys.AccessToken], StringComparison.Ordinal);

    private async Task<Credentials> RefreshAsync(Account account, ITokenRefresher refresher, Func<Credentials, bool> stillNeeded,
        CancellationToken cancellationToken)
    {
        await using var held = await gate.EnterAsync(account.Id, cancellationToken).ConfigureAwait(false);

        // Read again under the lock: a refresh that finished while this caller waited (here or in the other process) has stored new values.
        var stored = await credentials.GetAsync(account.Id, cancellationToken).ConfigureAwait(false);
        if (!stillNeeded(stored))
        {
            logger.LogInformation("Tokens of account {AccountId} ({Platform}) were already refreshed by another task", account.Id, account.Platform);
            return stored;
        }

        try
        {
            var fresh = await refresher.RefreshAsync(stored, cancellationToken).ConfigureAwait(false);
            await credentials.SaveAsync(account.Id, fresh, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Tokens of account {AccountId} ({Platform}) refreshed", account.Id, account.Platform);
            return fresh;
        }
        catch (PlatformAuthException ex)
        {
            logger.LogWarning("Refreshing the tokens of account {AccountId} ({Platform}) was refused: {Reason}", account.Id, account.Platform,
                Core.Security.Redactor.Redact(ex.Message));
            await MarkReconnectAsync(account, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
