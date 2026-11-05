using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>
/// The user pastes a token from the platform's own token tool (Meta Graph API Explorer, LinkedIn token generator) and, where needed, the
/// app's id and secret to turn it into a long-lived token. The platform writes the conversion and the identity read.
/// </summary>
public abstract class PastedTokenConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.PastedToken;

    /// <inheritdoc />
    protected override async Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        prompt.Status("connect.exchanging");
        return await ConvertAsync(input, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Turns the pasted values into stored credentials (long-lived token, page token...).</summary>
    protected abstract Task<Credentials> ConvertAsync(ConnectionInput input, CancellationToken cancellationToken);
}
