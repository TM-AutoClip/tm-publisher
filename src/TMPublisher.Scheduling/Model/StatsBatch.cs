using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Stats;

namespace TMPublisher.Scheduling;

/// <summary>Published targets of one account whose counters can be read.</summary>
public sealed record StatsBatch(Account Account, IStatsReader Reader, IReadOnlyList<PostTarget> Targets);
