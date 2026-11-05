namespace TMPublisher.Core.Accounts;

/// <summary>Secrets of the accounts, in their own table of the encrypted database.</summary>
public interface ICredentialStore
{
    /// <summary>The account's secrets (<see cref="Credentials.Empty"/> when none).</summary>
    Task<Credentials> GetAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Replaces the account's secrets. Called right after a token refresh, so a rotated refresh token is never lost.</summary>
    Task SaveAsync(Guid accountId, Credentials credentials, CancellationToken cancellationToken);
}
