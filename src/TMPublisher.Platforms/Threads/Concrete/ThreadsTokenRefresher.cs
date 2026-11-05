using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Threads;

/// <summary>
/// Threads long-lived tokens last 60 days and can be refreshed with themselves (<c>th_refresh_token</c>) once they are a day old. The app
/// refreshes when fewer than ten days are left, so a token is renewed long before it expires while the app is used.
/// </summary>
public sealed class ThreadsTokenRefresher(HttpTransport transport, TimeProvider clock) : ITokenRefresher
{
    private readonly PlatformHttp _http = new(transport.CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public string Platform => PlatformCodes.Threads;

    /// <inheritdoc />
    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return credentials.AccessExpiresAtUtc is { } expiry && expiry - nowUtc < TimeSpan.FromDays(10);
    }

    /// <inheritdoc />
    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var answer = await _http.GetJsonAsync("Threads token refresh", ThreadsApi.TokenRoot + "refresh_access_token?grant_type=th_refresh_token&access_token="
            + Uri.EscapeDataString(credentials.Require(CredentialKeys.AccessToken)), null, cancellationToken).ConfigureAwait(false);
        return OAuthTokens.Apply(credentials, answer, clock.GetUtcNow());
    }

    /// <summary>Turns a short-lived token into a 60-day one (needs the app secret).</summary>
    public async Task<Credentials> LongLivedAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var answer = await _http.GetJsonAsync("Threads long-lived token", ThreadsApi.TokenRoot + "access_token?grant_type=th_exchange_token&client_secret="
            + Uri.EscapeDataString(credentials.Require(CredentialKeys.ClientSecret)) + "&access_token=" + Uri.EscapeDataString(credentials.Require(CredentialKeys.AccessToken)),
            null, cancellationToken).ConfigureAwait(false);
        return OAuthTokens.Apply(credentials, answer, clock.GetUtcNow());
    }
}
