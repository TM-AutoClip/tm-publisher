using System.Text.RegularExpressions;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Discord;

/// <summary>Webhook address: the test reads the webhook (name, channel, server).</summary>
public sealed partial class DiscordConnection(HttpTransport transport, TimeProvider clock) : SecretUrlConnectionBase(transport, clock)
{
    /// <inheritdoc />
    protected override bool IsValid(string url) => Webhook().IsMatch(url);

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var hook = await Http.GetJsonAsync("Discord webhook", credentials.Require(CredentialKeys.WebhookUrl), null, cancellationToken).ConfigureAwait(false);
        return new AccountIdentity(JsonSafe.Str(hook, "id"), JsonSafe.Str(hook, "name"), "#" + JsonSafe.Str(hook, "channel_id"));
    }

    [GeneratedRegex(@"^https://(?:(?:canary|ptb)\.)?discord(?:app)?\.com/api/(?:v\d+/)?webhooks/\d+/[\w-]+$")]
    private static partial Regex Webhook();
}
