using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.TikTok;

/// <summary>TikTok tokens: access 24 hours, refresh about a year; the refresh token may change and is stored at once by the caller.</summary>
public sealed class TikTokTokenRefresher(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => PlatformCodes.TikTok;

    /// <inheritdoc />
    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc) => OAuthTokens.ExpiresSoon(credentials, nowUtc);

    /// <inheritdoc />
    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var reply = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Post, TikTokApi.Token)
        {
            Content = new FormUrlEncodedContent(
            [
                new("client_key", credentials.Require(CredentialKeys.ClientId)), new("client_secret", credentials[CredentialKeys.ClientSecret]),
                new("grant_type", "refresh_token"), new("refresh_token", credentials.Require(CredentialKeys.RefreshToken)),
            ]),
        }, cancellationToken).ConfigureAwait(false);
        var root = JsonSafe.TryParse(reply.Body);
        if (!reply.IsSuccess || JsonSafe.Str(root, "access_token").Length == 0)
        {
            var error = JsonSafe.Str(root, "error");
            throw error is "invalid_grant" or "invalid_client"
                ? new PlatformAuthException($"TikTok no longer accepts this account's access ({error}); connect it again.")
                : new PlatformRejectedException($"TikTok token refresh failed (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}");
        }

        return OAuthTokens.Apply(credentials, root, clock.GetUtcNow());
    }
}
