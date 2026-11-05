using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Stats;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;
using TMPublisher.Scheduling;
using TMPublisher.Core.Accounts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// History of the last 30 days with links, local counts (sent / failed per platform), quota use per account, and the platforms' counters
/// read only when the user clicks "Fetch" (the X cost is shown on the button first).
/// </summary>
public sealed partial class HistoryViewModel(IPostStore posts, IAccountStore accounts, IPlatformDirectory platforms, IPublishLog log, QuotaChecker quotas,
    StatsService stats, BrowserLauncher browser, TimeProvider clock, Localizer text, ILogger<HistoryViewModel> logger) : PageViewModelBase
{
    private IReadOnlyList<StatsBatch> _plan = [];

    /// <inheritdoc />
    public override string TitleKey => "nav.history";

    /// <inheritdoc />
    public override string Glyph => "≡";

    /// <inheritdoc />
    public override int Order => 30;

    /// <summary>Posts, newest first.</summary>
    public ObservableCollection<HistoryRowViewModel> Rows { get; } = [];

    /// <summary>Sent / failed per platform.</summary>
    public ObservableCollection<string> Counts { get; } = [];

    /// <summary>Quota use per account.</summary>
    public ObservableCollection<string> Quotas { get; } = [];

    /// <summary>Text of the fetch button (with the X cost when there is one).</summary>
    [ObservableProperty]
    public partial string FetchLabel { get; set; } = string.Empty;

    /// <summary>Something can be fetched.</summary>
    [ObservableProperty]
    public partial bool CanFetch { get; set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        var now = clock.GetUtcNow();
        var accountList = (await accounts.ListAsync(CancellationToken.None).ConfigureAwait(true)).ToDictionary(a => a.Id);
        var recent = await posts.ListAsync(now - StatsService.Window, now + TimeSpan.FromDays(366), CancellationToken.None).ConfigureAwait(true);
        Rows.Clear();
        foreach (var post in recent.Where(p => p.Status is not (PostStatus.Draft or PostStatus.Scheduled)).OrderByDescending(p => p.ScheduledAtUtc ?? p.CreatedAtUtc))
        {
            var targets = post.Targets.OrderBy(t => t.Order).Select(t => new HistoryTargetViewModel(t, Name(t.AccountId, accountList),
                text["target." + t.Status.ToString().ToLowerInvariant()] + (t.Message is { Length: > 0 } m ? " — " + m : string.Empty))).ToList();
            var summary = (post.Title.Length > 0 ? post.Title : post.Body).ReplaceLineEndings(" ");
            Rows.Add(new HistoryRowViewModel(TimeZoneInfo.ConvertTime(post.ScheduledAtUtc ?? post.CreatedAtUtc, clock.LocalTimeZone).ToString("g", text.Culture),
                summary.Length > 90 ? summary[..90] + "…" : summary, text["post." + post.Status.ToString().ToLowerInvariant()], targets));
        }

        Counts.Clear();
        foreach (var group in (await log.ListAsync(null, now - StatsService.Window, CancellationToken.None).ConfigureAwait(true)).GroupBy(e => e.Platform).OrderBy(g => g.Key))
        {
            Counts.Add(text.Format("history.count", platforms.Find(group.Key)?.DisplayName ?? group.Key, group.Count(e => e.Success), group.Count(e => !e.Success)));
        }

        Quotas.Clear();
        foreach (var account in accountList.Values.OrderBy(a => a.Platform))
        {
            if (platforms.Find(account.Platform) is not { } platform)
            {
                continue;
            }

            foreach (var rule in platform.Capabilities.Quotas)
            {
                var used = await quotas.UsedAsync(account, rule, CancellationToken.None).ConfigureAwait(true);
                Quotas.Add(text.Format("history.quota", platform.DisplayName, account.DisplayName, used, rule.MaxPosts, text["quota." + rule.Window.ToString().ToLowerInvariant()]));
            }
        }

        _plan = await stats.PlanAsync(CancellationToken.None).ConfigureAwait(true);
        var items = _plan.Sum(b => b.Targets.Count);
        var cost = StatsService.EstimateCost(_plan);
        CanFetch = items > 0;
        FetchLabel = cost > 0
            ? text.Format("history.fetch_cost", items, cost.ToString("0.###", CultureInfo.InvariantCulture))
            : text.Format("history.fetch", items);
        Status = Rows.Count == 0 ? text["history.none"] : string.Empty;
    }

    /// <summary>Reads the counters (the only request this page makes).</summary>
    [RelayCommand]
    public async Task FetchAsync()
    {
        try
        {
            var result = await stats.FetchAsync(_plan, CancellationToken.None).ConfigureAwait(true);
            foreach (var target in Rows.SelectMany(r => r.Targets))
            {
                if (result.ByTarget.TryGetValue(target.Target.Id, out var counters))
                {
                    target.Stats = Describe(counters);
                }
            }

            Status = result.Errors.Count == 0 ? text.Format("history.fetched", result.ByTarget.Count) : text.Format("history.fetch_errors", string.Join("; ", result.Errors));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reading counters failed");
            Status = text.Format("history.fetch_errors", ex.Message);
        }
    }

    /// <summary>Opens the published item in the browser.</summary>
    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps)
        {
            browser.Open(address);
        }
    }

    private string Describe(PostStats counters)
    {
        var parts = new List<string>();
        void Add(long? value, string key)
        {
            if (value is { } n)
            {
                parts.Add(text.Format(key, n.ToString("N0", text.Culture)));
            }
        }

        Add(counters.Views, "stats.views");
        Add(counters.Likes, "stats.likes");
        Add(counters.Reposts, "stats.reposts");
        Add(counters.Replies, "stats.replies");
        return string.Join(" · ", parts);
    }

    private string Name(Guid accountId, Dictionary<Guid, Account> accountList) =>
        accountList.TryGetValue(accountId, out var a) ? (platforms.Find(a.Platform)?.DisplayName ?? a.Platform) + " · " + a.DisplayName : text["attention.removed_account"];
}
