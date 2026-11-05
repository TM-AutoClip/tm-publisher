using TMPublisher.Core.Stats;

namespace TMPublisher.Scheduling;

/// <summary>Counters by target id, and the accounts that could not be read (with the reason).</summary>
public sealed record StatsResult(IReadOnlyDictionary<Guid, PostStats> ByTarget, IReadOnlyList<string> Errors);
