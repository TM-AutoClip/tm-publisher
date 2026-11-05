using TMPublisher.Core.Accounts;
using TMPublisher.Core.Publishing;

namespace TMPublisher.Tests.Scheduling;

/// <summary>A refresher like X's: every refresh token works once; a used one is refused (as X answers HTTP 400).</summary>
internal sealed class RotatingRefresher(string platform) : ITokenRefresher
{
    private int _calls;

    public string Platform => platform;

    public int Calls => _calls;

    public string ValidRefreshToken { get; private set; } = "r-0";

    public bool NeedsRefresh(Credentials credentials, DateTimeOffset nowUtc) => credentials[CredentialKeys.AccessToken].StartsWith("t-", StringComparison.Ordinal);

    public async Task<Credentials> RefreshAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        var used = credentials[CredentialKeys.RefreshToken];
        await Task.Delay(30, cancellationToken);
        if (used != ValidRefreshToken)
        {
            throw new PlatformAuthException("refresh token already used");
        }

        var n = Interlocked.Increment(ref _calls);
        ValidRefreshToken = "r-" + n;
        return credentials.With((CredentialKeys.AccessToken, "fresh-" + n), (CredentialKeys.RefreshToken, ValidRefreshToken));
    }
}
