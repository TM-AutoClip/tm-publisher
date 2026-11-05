using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>Instagram through Facebook Login: the Page's linked professional account; the test reads its username.</summary>
public sealed class InstagramConnection(HttpTransport transport, TimeProvider clock) : MetaPageConnectionBase(transport, clock)
{
    /// <inheritdoc />
    protected override Credentials Validate(Credentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return credentials[MetaKeys.InstagramId].Length > 0
            ? credentials.With((CredentialKeys.UserId, credentials[MetaKeys.InstagramId]))
            : throw new PlatformRejectedException("This Page has no linked Instagram professional account (or the token lacks instagram_basic). "
                + "Link the account to the Page in the Instagram app and allow instagram_basic and instagram_content_publish.");
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var account = await Http.GetJsonAsync("Instagram account", MetaGraph.Root + Uri.EscapeDataString(credentials.Require(MetaKeys.InstagramId))
            + "?fields=id,username,name&access_token=" + Uri.EscapeDataString(credentials.Require(CredentialKeys.AccessToken)), null, cancellationToken).ConfigureAwait(false);
        var username = JsonSafe.Str(account, "username");
        return new AccountIdentity(JsonSafe.Str(account, "id"), JsonSafe.Str(account, "name") is { Length: > 0 } n ? n : username, "@" + username);
    }
}
