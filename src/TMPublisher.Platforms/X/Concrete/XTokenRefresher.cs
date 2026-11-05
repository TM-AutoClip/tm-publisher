using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.X;

/// <summary>X OAuth 2.0 refresh (public client: only the client id). X rotates the refresh token: the new one is stored at once by the caller.</summary>
public sealed class XTokenRefresher(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    /// <summary>Token endpoint.</summary>
    public const string TokenEndpoint = "https://api.x.com/2/oauth2/token";

    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => PlatformCodes.X;

    /// <inheritdoc />
    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc) => OAuthTokens.ExpiresSoon(credentials, nowUtc);

    /// <inheritdoc />
    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var reply = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new("grant_type", "refresh_token"), new("refresh_token", credentials.Require(CredentialKeys.RefreshToken)),
                new("client_id", credentials.Require(CredentialKeys.ClientId)),
            ]),
        }, cancellationToken).ConfigureAwait(false);
        return reply.IsSuccess
            ? OAuthTokens.Apply(credentials, JsonSafe.TryParse(reply.Body), clock.GetUtcNow())
            : reply.Status is 400 or 401
                ? throw new PlatformAuthException($"X no longer accepts this account's access (HTTP {reply.Status}); connect it again.")
                : throw new PlatformRejectedException($"X token refresh failed (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}");
    }
}
