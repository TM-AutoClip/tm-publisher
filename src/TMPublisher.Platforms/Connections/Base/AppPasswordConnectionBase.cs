using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>Handle + app password (no developer app needed). The platform writes the session creation.</summary>
public abstract class AppPasswordConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.AppPassword;

    /// <inheritdoc />
    protected override Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken) =>
        CreateSessionAsync(input, cancellationToken);

    /// <summary>Signs in with the app password and returns the session tokens (the app password itself is kept to sign in again).</summary>
    protected abstract Task<Credentials> CreateSessionAsync(ConnectionInput input, CancellationToken cancellationToken);
}
