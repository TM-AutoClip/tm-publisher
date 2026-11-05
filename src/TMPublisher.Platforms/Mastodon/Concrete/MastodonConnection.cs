using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Mastodon;

/// <summary>
/// Registers an app on the server (<c>POST /api/v1/apps</c>, out-of-band return address so the code is shown on the page), signs in in the
/// browser, exchanges the pasted code. Mastodon tokens do not expire.
/// </summary>
public sealed class MastodonConnection(HttpTransport transport, TimeProvider clock) : RegisteredAppConnectionBase(transport, clock)
{
    /// <summary>Out-of-band return address.</summary>
    public const string OutOfBand = "urn:ietf:wg:oauth:2.0:oob";

    /// <summary>Requested permissions.</summary>
    public const string Scopes = "read:accounts write:statuses write:media";

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields => [new(CredentialKeys.Server, "field.mastodon_server", HintKey: "field.mastodon_server.hint")];

    /// <summary>The server's address from what the user typed ("mastodon.social" → https://mastodon.social).</summary>
    public static string Server(string typed)
    {
        var value = (typed ?? string.Empty).Trim().TrimEnd('/');
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value.Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host.Contains('.', StringComparison.Ordinal)
            ? uri.GetLeftPart(UriPartial.Authority)
            : throw new PlatformRejectedException("Write your server's address, for example mastodon.social.");
    }

    /// <inheritdoc />
    protected override async Task<Credentials> RegisterAsync(ConnectionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var server = Server(input[CredentialKeys.Server]);
        var app = await Http.PostFormAsync("Mastodon app registration", server + "/api/v1/apps",
            [new("client_name", "TM Publisher"), new("redirect_uris", OutOfBand), new("scopes", Scopes)], null, cancellationToken).ConfigureAwait(false);
        return Credentials.Empty.With((CredentialKeys.Server, server), (CredentialKeys.ClientId, JsonSafe.Str(app, "client_id")),
            (CredentialKeys.ClientSecret, JsonSafe.Str(app, "client_secret")));
    }

    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri(app[CredentialKeys.Server] + "/oauth/authorize?response_type=code&client_id=" + Uri.EscapeDataString(app[CredentialKeys.ClientId])
            + "&redirect_uri=" + Uri.EscapeDataString(OutOfBand) + "&scope=" + Uri.EscapeDataString(Scopes));
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("Mastodon sign-in", app[CredentialKeys.Server] + "/oauth/token",
        [
            new("grant_type", "authorization_code"), new("code", code), new("client_id", app[CredentialKeys.ClientId]),
            new("client_secret", app[CredentialKeys.ClientSecret]), new("redirect_uri", OutOfBand), new("scope", Scopes),
        ], null, cancellationToken).ConfigureAwait(false);
        return OAuthTokens.Apply(app, answer, Clock.GetUtcNow());
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var me = await Http.GetJsonAsync("Mastodon account", credentials[CredentialKeys.Server] + "/api/v1/accounts/verify_credentials",
            credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        var name = JsonSafe.Str(me, "display_name") is { Length: > 0 } d ? d : JsonSafe.Str(me, "username");
        return new AccountIdentity(JsonSafe.Str(me, "id"), name, "@" + JsonSafe.Str(me, "acct") + "@" + new Uri(credentials[CredentialKeys.Server]).Host);
    }
}
