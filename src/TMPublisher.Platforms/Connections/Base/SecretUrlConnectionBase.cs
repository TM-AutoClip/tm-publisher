using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Platforms.Connections;

/// <summary>A secret address that is itself the key (Discord webhook). The platform checks the address form and reads its identity.</summary>
public abstract class SecretUrlConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.SecretUrl;

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields => [new(CredentialKeys.WebhookUrl, "field.webhook_url", Secret: true)];

    /// <inheritdoc />
    protected override Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var url = input[CredentialKeys.WebhookUrl];
        return IsValid(url)
            ? Task.FromResult(Credentials.Empty.With((CredentialKeys.WebhookUrl, url)))
            : throw new PlatformRejectedException("This is not a valid address for this platform; copy it again from the platform.");
    }

    /// <summary>Whether the address has the platform's form.</summary>
    protected abstract bool IsValid(string url);
}
