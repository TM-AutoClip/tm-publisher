using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Stats;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.X;

/// <summary><c>GET /2/tweets?tweet.fields=public_metrics</c>, 100 ids per request; X charges per post read.</summary>
public sealed class XStatsReader(HttpTransport transport) : StatsReaderBase(transport)
{
    /// <summary>Price of one post read (pay-per-use).</summary>
    public const decimal ReadCost = 0.005m;

    /// <inheritdoc />
    public override string Platform => PlatformCodes.X;

    /// <inheritdoc />
    public override decimal CostPerItem => ReadCost;

    /// <inheritdoc />
    public override int BatchSize => 100;

    /// <inheritdoc />
    public override async Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(ids);
        var answer = await Http.GetJsonAsync("X metrics", XPublisher.Api + "tweets?tweet.fields=public_metrics&ids=" + Uri.EscapeDataString(string.Join(',', ids)),
            credentials.Require(CredentialKeys.AccessToken), cancellationToken).ConfigureAwait(false);
        return (answer?["data"] as JsonArray ?? []).ToDictionary(i => JsonSafe.Str(i, "id"), i =>
        {
            var m = i?["public_metrics"];
            return new PostStats(Number(m, "impression_count"), Number(m, "like_count"), Number(m, "retweet_count"), Number(m, "reply_count"));
        });
    }
}
