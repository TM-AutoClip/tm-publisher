using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Pinterest;

/// <summary>Pinterest tokens: access 30 days, refresh 60 days; refreshed with the app id and secret (Basic authorization).</summary>
public sealed class PinterestTokenRefresher(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => PlatformCodes.Pinterest;

    /// <inheritdoc />
    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return credentials.AccessExpiresAtUtc is { } expiry && expiry - nowUtc < TimeSpan.FromDays(2);
    }

    /// <inheritdoc />
    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var reply = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Post, PinterestApi.Root + "oauth/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "refresh_token"), new("refresh_token", credentials.Require(CredentialKeys.RefreshToken))]),
            Headers = { Authorization = PlatformHttp.Basic(credentials.Require(CredentialKeys.ClientId), credentials.Require(CredentialKeys.ClientSecret)) },
        }, cancellationToken).ConfigureAwait(false);
        return reply.IsSuccess
            ? OAuthTokens.Apply(credentials, JsonSafe.TryParse(reply.Body), clock.GetUtcNow())
            : reply.Status is 400 or 401
                ? throw new PlatformAuthException($"Pinterest no longer accepts this account's access (HTTP {reply.Status}); connect it again.")
                : throw new PlatformRejectedException($"Pinterest token refresh failed (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}");
    }
}
