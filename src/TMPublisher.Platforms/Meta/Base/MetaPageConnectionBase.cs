using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Meta;

/// <summary>
/// Facebook Login with the user's own Meta app, without a web server: the user makes a user token in Graph API Explorer for the own app,
/// pastes it with the app id and secret; the app turns it into a long-lived token (<c>fb_exchange_token</c>, about 60 days) and reads the
/// Page's token from <c>/me/accounts</c>. A Page token taken from a long-lived user token does not expire. Instagram uses the same Page and
/// its linked professional account.
/// </summary>
public abstract class MetaPageConnectionBase(HttpTransport transport, TimeProvider clock) : PastedTokenConnectionBase(transport, clock)
{
    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        new(CredentialKeys.ClientId, "field.meta_app_id"),
        new(CredentialKeys.ClientSecret, "field.meta_app_secret", Secret: true),
        new(CredentialKeys.UserToken, "field.meta_user_token", Secret: true, HintKey: "field.meta_user_token.hint"),
        new(CredentialKeys.PageId, "field.meta_page_id", Required: false, HintKey: "field.meta_page_id.hint"),
    ];

    /// <inheritdoc />
    protected override async Task<Credentials> ConvertAsync(ConnectionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var exchange = await Http.GetJsonAsync("Meta long-lived token", MetaGraph.Root + "oauth/access_token?grant_type=fb_exchange_token"
            + "&client_id=" + Uri.EscapeDataString(input[CredentialKeys.ClientId]) + "&client_secret=" + Uri.EscapeDataString(input[CredentialKeys.ClientSecret])
            + "&fb_exchange_token=" + Uri.EscapeDataString(input[CredentialKeys.UserToken]), null, cancellationToken).ConfigureAwait(false);
        var longToken = JsonSafe.Str(exchange, "access_token");
        if (longToken.Length == 0)
        {
            throw new PlatformRejectedException("Meta did not return a long-lived token; check the app id, the secret and the token.");
        }

        var pages = await Http.GetJsonAsync("Meta pages", MetaGraph.Root + "me/accounts?limit=100&fields="
            + Uri.EscapeDataString("id,name,access_token,instagram_business_account{id,username}") + "&access_token=" + Uri.EscapeDataString(longToken),
            null, cancellationToken).ConfigureAwait(false);
        var page = PickPage(pages?["data"] as JsonArray ?? [], input[CredentialKeys.PageId]);
        var credentials = Credentials.Empty.With(
            (CredentialKeys.ClientId, input[CredentialKeys.ClientId]),
            (CredentialKeys.ClientSecret, input[CredentialKeys.ClientSecret]),
            (CredentialKeys.PageId, JsonSafe.Str(page, "id")),
            (CredentialKeys.AccessToken, JsonSafe.Str(page, "access_token")),
            (MetaKeys.InstagramId, JsonSafe.Str(page?["instagram_business_account"], "id")));
        return Validate(credentials);
    }

    /// <summary>Checks the converted credentials (Instagram: a linked professional account must exist).</summary>
    protected virtual Credentials Validate(Credentials credentials) => credentials;

    /// <summary>The Page: the one with the given id, or the only one; otherwise an error that lists the Pages.</summary>
    private static JsonNode? PickPage(JsonArray pages, string pageId)
    {
        if (pages.Count == 0)
        {
            throw new PlatformRejectedException("The token sees no Facebook Page. In Graph API Explorer add pages_show_list and choose your Page when allowing access.");
        }

        if (pageId.Length > 0)
        {
            return pages.FirstOrDefault(p => JsonSafe.Str(p, "id") == pageId)
                ?? throw new PlatformRejectedException("The token does not see the Page " + pageId + ". Pages it sees: " + List(pages));
        }

        return pages.Count == 1
            ? pages[0]
            : throw new PlatformRejectedException("The token sees several Pages; write the Page ID of the one to use: " + List(pages));
    }

    private static string List(JsonArray pages) => string.Join(", ", pages.Select(p => JsonSafe.Str(p, "name") + " (" + JsonSafe.Str(p, "id") + ")"));
}
