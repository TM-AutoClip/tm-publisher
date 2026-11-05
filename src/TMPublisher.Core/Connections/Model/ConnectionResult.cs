using TMPublisher.Core.Accounts;

namespace TMPublisher.Core.Connections;

/// <summary>
/// A finished connection: the identity read from the platform, the secrets to store, and (for platforms without automatic renewal) when
/// the user must connect again.
/// </summary>
public sealed record ConnectionResult(AccountIdentity Identity, Credentials Credentials, DateTimeOffset? ReconnectByUtc = null);
