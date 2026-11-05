using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Stats;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>The public AppView's <c>app.bsky.feed.getPosts</c> (no sign-in needed), 25 uris per request.</summary>
public sealed class BlueskyStatsReader(HttpTransport transport) : StatsReaderBase(transport)
{
    /// <summary>Public AppView.</summary>
    public const string AppView = "https://public.api.bsky.app/xrpc/app.bsky.feed.getPosts";

    /// <inheritdoc />
    public override string Platform => PlatformCodes.Bluesky;

    /// <inheritdoc />
    public override int BatchSize => 25;

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var answer = await Http.GetJsonAsync("Bluesky posts", AppView + "?" + string.Join('&', ids.Select(u => "uris=" + Uri.EscapeDataString(u))), null, cancellationToken)
            .ConfigureAwait(false);
        return (answer?["posts"] as JsonArray ?? []).ToDictionary(p => JsonSafe.Str(p, "uri"),
            p => new PostStats(null, Number(p, "likeCount"), Number(p, "repostCount"), Number(p, "replyCount")));
    }
}
