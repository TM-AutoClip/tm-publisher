using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Stats;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.YouTube;

/// <summary><c>videos.list?part=statistics</c>, 50 ids per request (1 quota unit).</summary>
public sealed class YouTubeStatsReader(HttpTransport transport) : StatsReaderBase(transport)
{
    /// <inheritdoc />
    public override string Platform => PlatformCodes.YouTube;

    /// <inheritdoc />
    public override int BatchSize => 50;

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(ids);
        var answer = await Http.GetJsonAsync("YouTube statistics", YouTubePublisher.Api + "videos?part=statistics&id=" + Uri.EscapeDataString(string.Join(',', ids)),
            credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        return (answer?["items"] as JsonArray ?? []).ToDictionary(i => JsonSafe.Str(i, "id"), i =>
        {
            var s = i?["statistics"];
            return new PostStats(Number(s, "viewCount"), Number(s, "likeCount"), null, Number(s, "commentCount"));
        });
    }
}
