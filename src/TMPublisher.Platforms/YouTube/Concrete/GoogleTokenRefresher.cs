using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.YouTube;

/// <summary>Google OAuth tokens: refresh with the user's own desktop client (id + secret; Google does not rotate the refresh token).</summary>
public sealed class GoogleTokenRefresher(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    /// <summary>Token endpoint.</summary>
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => PlatformCodes.YouTube;

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
                new("client_id", credentials.Require(CredentialKeys.ClientId)), new("client_secret", credentials[CredentialKeys.ClientSecret]),
            ]),
        }, cancellationToken).ConfigureAwait(false);
        if (reply.Status is 400 or 401 && JsonSafe.Str(JsonSafe.TryParse(reply.Body), "error") is "invalid_grant" or "invalid_client" or "unauthorized_client")
        {
            throw new PlatformAuthException("Google no longer accepts this account's access (" + JsonSafe.Str(JsonSafe.TryParse(reply.Body), "error")
                + "); connect it again. A project in 'Testing' drops access after 7 days.");
        }

        return reply.IsSuccess
            ? OAuthTokens.Apply(credentials, JsonSafe.TryParse(reply.Body), clock.GetUtcNow())
            : throw new PlatformRejectedException($"Google token refresh failed (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}");
    }
}
