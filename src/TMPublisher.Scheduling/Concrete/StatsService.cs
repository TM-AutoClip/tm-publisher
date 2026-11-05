using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Security;
using TMPublisher.Core.Stats;
using TMPublisher.Scheduling.Accounts;

namespace TMPublisher.Scheduling;

/// <summary>
/// Counters of what this program published in the last 30 days, read only when the user asks. The plan (and the X cost estimate) is
/// built without any request.
/// </summary>
public sealed class StatsService(IPostStore posts, IAccountStore accounts, IPlatformDirectory platforms, IEnumerable<IStatsReader> readers,
    CredentialRenewer renewer, TimeProvider clock, ILogger<StatsService> logger)
{
    /// <summary>How far back.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    private readonly Dictionary<string, IStatsReader> _readers = readers.ToDictionary(r => r.Platform, StringComparer.Ordinal);

    /// <summary>Whether counters can be read for the platform.</summary>
    public bool Supports(string platform) => _readers.ContainsKey(platform);

    /// <summary>What would be read (local only).</summary>
    public async Task<IReadOnlyList<StatsBatch>> PlanAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var recent = await posts.ListAsync(now - Window, now + TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var accountList = (await accounts.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.Id);
        return recent.SelectMany(p => p.Targets)
            .Where(t => t.Status == TargetStatus.Published && t.ExternalId is { Length: > 0 } && accountList.ContainsKey(t.AccountId))
            .GroupBy(t => t.AccountId)
            .Select(g => (Account: accountList[g.Key], Targets: g.ToList()))
            .Where(x => _readers.ContainsKey(x.Account.Platform) && platforms.Find(x.Account.Platform) is not null)
            .Select(x => new StatsBatch(x.Account, _readers[x.Account.Platform], x.Targets))
            .ToList();
    }

    /// <summary>Estimated cost of reading the plan in US dollars.</summary>
    public static decimal EstimateCost(IEnumerable<StatsBatch> plan) => (plan ?? []).Sum(b => b.Reader.CostPerItem * b.Targets.Count);

    /// <summary>Reads the counters (the requests happen here).</summary>
    public async Task<StatsResult> FetchAsync(IReadOnlyList<StatsBatch> plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var byTarget = new Dictionary<Guid, PostStats>();
        var errors = new List<string>();
        foreach (var batch in plan)
        {
            try
            {
                var credentials = await renewer.GetFreshAsync(batch.Account, cancellationToken).ConfigureAwait(false);
                foreach (var chunk in batch.Targets.Chunk(Math.Max(1, batch.Reader.BatchSize)))
                {
                    var read = await batch.Reader.ReadAsync(credentials, [.. chunk.Select(t => t.ExternalId!)], cancellationToken).ConfigureAwait(false);
                    foreach (var target in chunk)
                    {
                        if (read.TryGetValue(target.ExternalId!, out var stats))
                        {
                            byTarget[target.Id] = stats;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var reason = Redactor.Redact(ex.Message);
                logger.LogWarning("Counters of account {AccountId} ({Platform}) could not be read ({Type}): {Reason}", batch.Account.Id, batch.Account.Platform,
                    ex.GetType().Name, reason);
                errors.Add(batch.Account.DisplayName + " (" + batch.Account.Platform + "): " + reason);
            }
        }

        logger.LogInformation("Counters read for {Count} items", byTarget.Count);
        return new StatsResult(byTarget, errors);
    }
}
