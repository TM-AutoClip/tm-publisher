using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Quotas;

namespace TMPublisher.Scheduling;

/// <summary>Local quota estimate: successful posts of the account in the platform's window, counted from the local publish log.</summary>
public sealed class QuotaChecker(IPublishLog log, TimeProvider clock)
{
    private static readonly TimeZoneInfo Pacific = FindPacific();

    /// <summary>The first rule that one more post would break; <c>null</c> when there is room.</summary>
    public async Task<QuotaRule?> FullRuleAsync(Account account, IPlatform platform, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(platform);
        foreach (var rule in platform.Capabilities.Quotas)
        {
            if (await UsedAsync(account, rule, cancellationToken).ConfigureAwait(false) >= rule.MaxPosts)
            {
                return rule;
            }
        }

        return null;
    }

    /// <summary>Successful posts of the account in the rule's current window.</summary>
    public async Task<int> UsedAsync(Account account, QuotaRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(rule);
        var since = WindowStart(rule.Window, clock.GetUtcNow());
        return (await log.ListAsync(account.Id, since, cancellationToken).ConfigureAwait(false)).Count(e => e.Success);
    }

    /// <summary>Start of the window that contains <paramref name="nowUtc"/>.</summary>
    public static DateTimeOffset WindowStart(QuotaWindow window, DateTimeOffset nowUtc) => window switch
    {
        QuotaWindow.Rolling15Minutes => nowUtc - TimeSpan.FromMinutes(15),
        QuotaWindow.PacificDay => PacificMidnight(nowUtc),
        _ => nowUtc - TimeSpan.FromHours(24),
    };

    private static DateTimeOffset PacificMidnight(DateTimeOffset nowUtc)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, Pacific);
        var midnight = local.Date;
        return new DateTimeOffset(midnight, Pacific.GetUtcOffset(midnight)).ToUniversalTime();
    }

    private static TimeZoneInfo FindPacific()
    {
        foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        // No time-zone data on this system: Pacific standard time without daylight saving (the window may start an hour early).
        return TimeZoneInfo.CreateCustomTimeZone("Pacific", TimeSpan.FromHours(-8), "Pacific", "Pacific");
    }
}
