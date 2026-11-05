using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;

namespace TMPublisher.Core.Connections;

/// <summary>
/// Connects an account with the user's own developer app or key, and tests stored credentials. Shared flows live in abstract bases
/// (loopback + PKCE, pasted redirect, pasted token, app password, registered app, secret url, bot token); a platform writes only its
/// differences.
/// </summary>
public interface IConnectionMethod
{
    /// <summary>Kind of flow.</summary>
    ConnectionKind Kind { get; }

    /// <summary>Values the user enters (everything in one window; connecting is not possible with a required field empty).</summary>
    IReadOnlyList<ConnectionField> Fields { get; }

    /// <summary>Runs the flow and reads the account identity (the connection test).</summary>
    Task<ConnectionResult> ConnectAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken);

    /// <summary>"Test connection": reads the identity with stored credentials (may refresh them; refreshed values are returned).</summary>
    Task<(AccountIdentity Identity, Credentials Credentials)> TestAsync(Credentials credentials, CancellationToken cancellationToken);
}
