using System.Text.Json.Nodes;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Stats;

namespace TMPublisher.Platforms.Common;

/// <summary>Shared part of the counter readers: one HTTP client, number parsing.</summary>
public abstract class StatsReaderBase(HttpTransport transport) : IStatsReader
{
    /// <summary>HTTP helpers.</summary>
    protected PlatformHttp Http { get; } = new((transport ?? throw new ArgumentNullException(nameof(transport))).CreateClient(TimeSpan.FromSeconds(60)));

    /// <inheritdoc />
    public abstract string Platform { get; }

    /// <inheritdoc />
    public virtual decimal CostPerItem => 0;

    /// <inheritdoc />
    public virtual int BatchSize => 1;

    /// <inheritdoc />
    public abstract Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>A counter that may come as a number or a string; <c>null</c> when missing.</summary>
    protected static long? Number(JsonNode? node, string name) =>
        node?[name] is JsonValue value && (value.TryGetValue<long>(out var n) || (value.TryGetValue<string>(out var s) && long.TryParse(s, out n))) ? n : null;
}
