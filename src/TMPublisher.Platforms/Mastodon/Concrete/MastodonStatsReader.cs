using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Stats;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Mastodon;

/// <summary><c>GET /api/v1/statuses/:id</c> on the account's server, one status per request.</summary>
public sealed class MastodonStatsReader(HttpTransport transport) : StatsReaderBase(transport)
{
    /// <inheritdoc />
    public override string Platform => PlatformCodes.Mastodon;

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(ids);
        var server = credentials.Require(CredentialKeys.Server).TrimEnd('/');
        var result = new Dictionary<string, PostStats>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var status = await Http.GetJsonAsync("Mastodon status", server + "/api/v1/statuses/" + Uri.EscapeDataString(id), credentials.Require(CredentialKeys.AccessToken),
                cancellationToken).ConfigureAwait(false);
            result[id] = new PostStats(null, Number(status, "favourites_count"), Number(status, "reblogs_count"), Number(status, "replies_count"));
        }

        return result;
    }
}
