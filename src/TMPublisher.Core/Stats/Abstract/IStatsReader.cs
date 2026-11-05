using TMPublisher.Core.Accounts;

namespace TMPublisher.Core.Stats;

/// <summary>Reads the counters of items this program published. Called only when the user clicks "Fetch".</summary>
public interface IStatsReader
{
    /// <summary>Platform code.</summary>
    string Platform { get; }

    /// <summary>Cost of reading one item in US dollars (X charges per read; 0 elsewhere).</summary>
    decimal CostPerItem { get; }

    /// <summary>Most items per request.</summary>
    int BatchSize { get; }

    /// <summary>Counters by the platform's item id; items the platform no longer returns are left out.</summary>
    Task<IReadOnlyDictionary<string, PostStats>> ReadAsync(Credentials credentials, IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
