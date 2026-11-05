using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Posts waiting for the user: missed (the computer was off at the time), blocked (a check failed before sending) and stopped (a target
/// failed while sending). Nothing here is sent without a click.
/// </summary>
public sealed partial class AttentionViewModel : PageViewModelBase, IPageInfo
{
    private readonly IPostStore _posts;
    private readonly IAccountStore _accounts;
    private readonly IPlatformDirectory _platforms;
    private readonly IPostScheduler _scheduler;
    private readonly TimeProvider _clock;
    private readonly Localizer _text;
    private readonly ILogger<AttentionViewModel> _logger;

    /// <summary>Creates.</summary>
    public AttentionViewModel(IPostStore posts, IAccountStore accounts, IPlatformDirectory platforms, IPostScheduler scheduler, TimeProvider clock, Localizer text,
        ILogger<AttentionViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _posts = posts;
        _accounts = accounts;
        _platforms = platforms;
        _scheduler = scheduler;
        _clock = clock;
        _text = text;
        _logger = logger;
        _scheduler.AttentionChanged += (_, _) => Dispatcher.UIThread.Post(() => _ = ReloadAsync());
    }

    /// <inheritdoc />
    public static string TitleKey => "nav.attention";

    /// <inheritdoc />
    public static string Glyph => "!";

    /// <inheritdoc />
    public static int Order => 15;

    /// <summary>Waiting posts, oldest first.</summary>
    public ObservableCollection<AttentionRowViewModel> Rows { get; } = [];

    /// <inheritdoc />
    public override Task ActivateAsync() => ReloadAsync();

    /// <summary>Reads the waiting posts again.</summary>
    public async Task ReloadAsync()
    {
        var waiting = await _posts.ListByStatusAsync([PostStatus.Missed, PostStatus.Blocked, PostStatus.Stopped], CancellationToken.None).ConfigureAwait(true);
        var accounts = (await _accounts.ListAsync(CancellationToken.None).ConfigureAwait(true)).ToDictionary(a => a.Id);
        Rows.Clear();
        foreach (var post in waiting.OrderBy(p => p.ScheduledAtUtc ?? p.UpdatedAtUtc))
        {
            var lines = post.Targets.OrderBy(t => t.Order).Select(t =>
            {
                var name = accounts.TryGetValue(t.AccountId, out var a) ? (_platforms.Find(a.Platform)?.DisplayName ?? a.Platform) + " · " + a.DisplayName : _text["attention.removed_account"];
                return name + ": " + _text["target." + t.Status.ToString().ToLowerInvariant()] + (t.Message is { Length: > 0 } m ? " — " + m : string.Empty);
            });
            var when = post.ScheduledAtUtc is { } at ? TimeZoneInfo.ConvertTime(at, _clock.LocalTimeZone).ToString("g", _text.Culture) : string.Empty;
            var summary = (post.Title.Length > 0 ? post.Title : post.Body).ReplaceLineEndings(" ");
            Rows.Add(new AttentionRowViewModel(post, summary.Length > 90 ? summary[..90] + "…" : summary, when,
                _text["attention." + post.Status.ToString().ToLowerInvariant()], string.Join(Environment.NewLine, lines))
            {
                Date = _clock.GetLocalNow().Date,
            });
        }

        Status = Rows.Count == 0 ? _text["attention.none"] : string.Empty;
    }

    /// <summary>Send now.</summary>
    [RelayCommand]
    public Task SendNowAsync(AttentionRowViewModel? row) => ApplyAsync(row, id => _scheduler.SendNowAsync(id, CancellationToken.None));

    /// <summary>Schedule again at the row's date and time.</summary>
    [RelayCommand]
    public Task RescheduleAsync(AttentionRowViewModel? row)
    {
        if (row?.Date is not { } date)
        {
            Status = _text["compose.pick_time"];
            return Task.CompletedTask;
        }

        var local = date.Date + (row.Time ?? TimeSpan.Zero);
        var at = new DateTimeOffset(local, _clock.LocalTimeZone.GetUtcOffset(local)).ToUniversalTime();
        if (at <= _clock.GetUtcNow())
        {
            Status = _text["compose.time_past"];
            return Task.CompletedTask;
        }

        return ApplyAsync(row, id => _scheduler.RescheduleAsync(id, at, CancellationToken.None));
    }

    /// <summary>Cancel what is not sent.</summary>
    [RelayCommand]
    public Task CancelAsync(AttentionRowViewModel? row) => ApplyAsync(row, id => _scheduler.CancelAsync(id, CancellationToken.None));

    /// <summary>Try the failed target again.</summary>
    [RelayCommand]
    public Task RetryAsync(AttentionRowViewModel? row) => ApplyAsync(row, id => _scheduler.RetryAsync(id, CancellationToken.None));

    /// <summary>Skip the failed target and continue.</summary>
    [RelayCommand]
    public Task SkipAsync(AttentionRowViewModel? row) => ApplyAsync(row, id => _scheduler.SkipFailedAsync(id, CancellationToken.None));

    private async Task ApplyAsync(AttentionRowViewModel? row, Func<Guid, Task<bool>> action)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            if (!await action(row.Post.Id).ConfigureAwait(true))
            {
                Status = _text["attention.changed"];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A decision on post {PostId} failed", row.Post.Id);
            Status = _text.Format("attention.failed", ex.Message);
        }

        await ReloadAsync().ConfigureAwait(true);
    }
}
