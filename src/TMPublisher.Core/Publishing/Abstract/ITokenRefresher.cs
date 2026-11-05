using TMPublisher.Core.Accounts;

namespace TMPublisher.Core.Publishing;

/// <summary>
/// Refreshes an account's tokens with the user's own app (the app does it, there is no server). Implemented by platforms whose tokens can
/// be refreshed; the caller stores the result immediately (a rotated refresh token must not be lost).
/// </summary>
public interface ITokenRefresher
{
    /// <summary>Platform code.</summary>
    string Platform { get; }

    /// <summary>Whether the credentials should be refreshed before use (expired or about to).</summary>
    bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc);

    /// <summary>New credentials; <see cref="PlatformAuthException"/> when the refresh token is no longer valid.</summary>
    Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken);
}
