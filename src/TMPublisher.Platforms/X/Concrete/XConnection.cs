using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.X;

/// <summary>X OAuth 2.0 authorization code with PKCE, public client (native app: no secret), offline access for a refresh token.</summary>
public sealed class XConnection(HttpTransport transport, TimeProvider clock, ILoopbackListener listener) : LoopbackPkceConnectionBase(transport, clock, listener)
{
    /// <summary>Permissions.</summary>
    public const string Scopes = "tweet.read tweet.write users.read media.write offline.access";

    /// <inheritdoc />
    protected override int Port => XPlatform.LoopbackPort;

    /// <inheritdoc />
    protected override bool NeedsSecret => false;

    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app, string state, string challenge)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri("https://x.com/i/oauth2/authorize?response_type=code&client_id=" + Uri.EscapeDataString(app[CredentialKeys.ClientId])
            + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&scope=" + Uri.EscapeDataString(Scopes) + "&state=" + Uri.EscapeDataString(state)
            + "&code_challenge=" + challenge + "&code_challenge_method=S256");
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, string verifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("X sign-in", XTokenRefresher.TokenEndpoint,
        [
            new("grant_type", "authorization_code"), new("code", code), new("redirect_uri", RedirectUri), new("code_verifier", verifier),
            new("client_id", app[CredentialKeys.ClientId]),
        ], null, cancellationToken).ConfigureAwait(false);
        return OAuthTokens.Apply(app, answer, Clock.GetUtcNow());
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var me = await Http.GetJsonAsync("X profile", XPublisher.Api + "users/me", credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(me?["data"], "id");
        var username = JsonSafe.Str(me?["data"], "username");
        return id.Length == 0
            ? throw new PlatformRejectedException("X returned no user id.")
            : new AccountIdentity(id, JsonSafe.Str(me?["data"], "name") is { Length: > 0 } n ? n : username, "@" + username);
    }
}
