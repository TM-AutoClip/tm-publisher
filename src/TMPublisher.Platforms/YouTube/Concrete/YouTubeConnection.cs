using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.YouTube;

/// <summary>
/// Google sign-in for installed apps: loopback address, PKCE "always use", offline access (refresh token), consent forced so a refresh token
/// is returned. The desktop client's secret is not secret by Google's own definition but is still stored encrypted.
/// </summary>
public sealed class YouTubeConnection(HttpTransport transport, TimeProvider clock, ILoopbackListener listener, GoogleTokenRefresher refresher)
    : LoopbackPkceConnectionBase(transport, clock, listener)
{
    /// <summary>Permissions: upload and read the channel / processing state.</summary>
    public const string Scopes = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly";

    /// <inheritdoc />
    protected override int Port => YouTubePlatform.LoopbackPort;

    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app, string state, string challenge)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri("https://accounts.google.com/o/oauth2/v2/auth?response_type=code&access_type=offline&prompt=consent&include_granted_scopes=true"
            + "&client_id=" + Uri.EscapeDataString(app[CredentialKeys.ClientId]) + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
            + "&scope=" + Uri.EscapeDataString(Scopes) + "&state=" + Uri.EscapeDataString(state)
            + "&code_challenge=" + challenge + "&code_challenge_method=S256");
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, string verifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("Google sign-in", GoogleTokenRefresher.TokenEndpoint,
        [
            new("grant_type", "authorization_code"), new("code", code), new("redirect_uri", RedirectUri), new("code_verifier", verifier),
            new("client_id", app[CredentialKeys.ClientId]), new("client_secret", app[CredentialKeys.ClientSecret]),
        ], null, cancellationToken).ConfigureAwait(false);
        var tokens = OAuthTokens.Apply(app, answer, Clock.GetUtcNow());
        return tokens[CredentialKeys.RefreshToken].Length > 0
            ? tokens
            : throw new PlatformRejectedException("Google returned no refresh token; remove TM Publisher's access in your Google account and connect again.");
    }

    /// <inheritdoc />
    public override async Task<(AccountIdentity Identity, Credentials Credentials)> TestAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        try
        {
            return (await ReadIdentityAsync(credentials, cancellationToken).ConfigureAwait(false), credentials);
        }
        catch (PlatformAuthException)
        {
            var fresh = await refresher.RefreshAsync(credentials, cancellationToken).ConfigureAwait(false);
            return (await ReadIdentityAsync(fresh, cancellationToken).ConfigureAwait(false), fresh);
        }
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var answer = await Http.GetJsonAsync("YouTube channel", YouTubePublisher.Api + "channels?part=snippet&mine=true",
            credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        var channel = answer?["items"]?[0] ?? throw new PlatformRejectedException("This Google account has no YouTube channel.");
        var id = JsonSafe.Str(channel, "id");
        var title = JsonSafe.Str(channel["snippet"], "title");
        var handle = JsonSafe.Str(channel["snippet"], "customUrl");
        return new AccountIdentity(id, title.Length > 0 ? title : id, handle.Length > 0 ? handle : null);
    }
}
