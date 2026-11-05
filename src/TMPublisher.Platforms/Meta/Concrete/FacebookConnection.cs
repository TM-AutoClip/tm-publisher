using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>Facebook Page connection: the test reads the Page with its own token.</summary>
public sealed class FacebookConnection(HttpTransport transport, TimeProvider clock) : MetaPageConnectionBase(transport, clock)
{
    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var page = await Http.GetJsonAsync("Facebook Page", MetaGraph.Root + Uri.EscapeDataString(credentials.Require(CredentialKeys.PageId))
            + "?fields=id,name,link&access_token=" + Uri.EscapeDataString(credentials.Require(CredentialKeys.AccessToken)), null, cancellationToken).ConfigureAwait(false);
        return new AccountIdentity(JsonSafe.Str(page, "id"), JsonSafe.Str(page, "name"), JsonSafe.Str(page, "link") is { Length: > 0 } link ? link : null);
    }
}
