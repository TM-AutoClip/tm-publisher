using System.Security.Cryptography;
using System.Text;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.TikTok;

/// <summary>Login Kit for Desktop: loopback (localhost / 127.0.0.1 only), PKCE required, the challenge is the HEX of SHA-256 (not base64url).</summary>
public sealed class TikTokConnection(HttpTransport transport, TimeProvider clock, ILoopbackListener listener) : LoopbackPkceConnectionBase(transport, clock, listener)
{
    /// <inheritdoc />
    protected override int Port => TikTokPlatform.LoopbackPort;

    /// <inheritdoc />
    protected override string Challenge(Pkce pkce)
    {
        ArgumentNullException.ThrowIfNull(pkce);
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(pkce.Verifier))).ToLowerInvariant();
    }

    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app, string state, string challenge)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri(TikTokApi.Authorize + "?client_key=" + Uri.EscapeDataString(app[CredentialKeys.ClientId]) + "&scope=" + Uri.EscapeDataString(TikTokApi.Scopes)
            + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(RedirectUri) + "&state=" + Uri.EscapeDataString(state)
            + "&code_challenge=" + challenge + "&code_challenge_method=S256");
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, string verifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("TikTok sign-in", TikTokApi.Token,
        [
            new("client_key", app[CredentialKeys.ClientId]), new("client_secret", app[CredentialKeys.ClientSecret]), new("code", code),
            new("grant_type", "authorization_code"), new("redirect_uri", RedirectUri), new("code_verifier", verifier),
        ], null, cancellationToken).ConfigureAwait(false);
        if (JsonSafe.Str(answer, "access_token").Length == 0)
        {
            throw new PlatformRejectedException("TikTok did not return a token: " + JsonSafe.Str(answer, "error_description"));
        }

        return OAuthTokens.Apply(app, answer, Clock.GetUtcNow());
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var answer = await Http.GetJsonAsync("TikTok profile", TikTokApi.Root + "user/info/?fields=open_id,display_name", credentials.Require(CredentialKeys.AccessToken),
            cancellationToken).ConfigureAwait(false);
        var user = answer?["data"]?["user"];
        var id = JsonSafe.Str(user, "open_id");
        return id.Length == 0
            ? throw new PlatformRejectedException("TikTok returned no user id.")
            : new AccountIdentity(id, JsonSafe.Str(user, "display_name") is { Length: > 0 } d ? d : id, null);
    }
}
