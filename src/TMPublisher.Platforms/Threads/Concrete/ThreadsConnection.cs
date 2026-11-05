using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Threads;

/// <summary>
/// Threads sign-in with the user's own Meta app: Threads needs an https return address, so the address the browser ends on is pasted back.
/// The short token is turned into a 60-day token at once.
/// </summary>
public sealed class ThreadsConnection(HttpTransport transport, TimeProvider clock, ThreadsTokenRefresher tokens) : PastedRedirectConnectionBase(transport, clock)
{
    /// <inheritdoc />
    protected override Uri AuthorizeUri(Credentials app, string state)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new Uri(ThreadsApi.Authorize + "?response_type=code&client_id=" + Uri.EscapeDataString(app[CredentialKeys.ClientId])
            + "&redirect_uri=" + Uri.EscapeDataString(app[CredentialKeys.RedirectUri]) + "&scope=" + Uri.EscapeDataString(ThreadsApi.Scopes)
            + "&state=" + Uri.EscapeDataString(state));
    }

    /// <inheritdoc />
    protected override async Task<Credentials> ExchangeAsync(Credentials app, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        var answer = await Http.PostFormAsync("Threads sign-in", ThreadsApi.TokenRoot + "oauth/access_token",
        [
            new("client_id", app[CredentialKeys.ClientId]), new("client_secret", app[CredentialKeys.ClientSecret]), new("grant_type", "authorization_code"),
            new("redirect_uri", app[CredentialKeys.RedirectUri]), new("code", code),
        ], null, cancellationToken).ConfigureAwait(false);
        var shortLived = OAuthTokens.Apply(app, answer, Clock.GetUtcNow()).With((CredentialKeys.UserId, JsonSafe.Str(answer, "user_id")));
        return await tokens.LongLivedAsync(shortLived, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var me = await Http.GetJsonAsync("Threads profile", ThreadsApi.Graph + "me?fields=id,username,name&access_token="
            + Uri.EscapeDataString(credentials.Require(CredentialKeys.AccessToken)), null, cancellationToken).ConfigureAwait(false);
        var username = JsonSafe.Str(me, "username");
        return new AccountIdentity(JsonSafe.Str(me, "id"), JsonSafe.Str(me, "name") is { Length: > 0 } n ? n : username, "@" + username);
    }
}
