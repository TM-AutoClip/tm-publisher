using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Month and week calendar of posts (local time). A planned post can be dragged to another day: the time of day stays, a time in the past
/// is refused. Double-click opens the post in the compose screen.
/// </summary>
public sealed partial class CalendarViewModel(IPostStore posts, IAccountStore accounts, IPlatformDirectory platforms, IPostScheduler scheduler,
    ComposeViewModel compose, TimeProvider clock, Localizer text, ILogger<CalendarViewModel> logger) : PageViewModelBase
{
    /// <inheritdoc />
    public override string TitleKey => "nav.calendar";

    /// <inheritdoc />
    public override string Glyph => "▦";

    /// <inheritdoc />
    public override int Order => 20;

    /// <summary>Day cells (42 in the month view, 7 in the week view), Monday first.</summary>
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    /// <summary>Weekday names, Monday first.</summary>
    public ObservableCollection<string> WeekdayNames { get; } = [];

    /// <summary>Week view instead of month view.</summary>
    [ObservableProperty]
    public partial bool IsWeek { get; set; }

    /// <summary>A date inside the shown range (local).</summary>
    [ObservableProperty]
    public partial DateTime Anchor { get; set; }

    /// <summary>"March 2026" or the week's first and last day.</summary>
    [ObservableProperty]
    public partial string RangeTitle { get; set; } = string.Empty;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (Anchor == default)
        {
            Anchor = Today();
        }

        return LoadAsync();
    }

    /// <summary>Reads the posts of the shown range.</summary>
    public async Task LoadAsync()
    {
        var first = IsWeek ? Monday(Anchor) : Monday(new DateTime(Anchor.Year, Anchor.Month, 1));
        var count = IsWeek ? 7 : 42;
        var found = await posts.ListAsync(ToUtc(first), ToUtc(first.AddDays(count)), CancellationToken.None).ConfigureAwait(true);
        var accountList = (await accounts.ListAsync(CancellationToken.None).ConfigureAwait(true)).ToDictionary(a => a.Id);
        var today = Today();
        WeekdayNames.Clear();
        for (var i = 0; i < 7; i++)
        {
            WeekdayNames.Add(text.Culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((i + 1) % 7)));
        }

        Days.Clear();
        for (var i = 0; i < count; i++)
        {
            var date = first.AddDays(i);
            Days.Add(new CalendarDayViewModel(date, IsWeek || date.Month == Anchor.Month, date == today));
        }

        foreach (var post in found)
        {
            var local = LocalOf(post);
            if (Days.FirstOrDefault(d => d.Date == local.Date) is { } day)
            {
                day.Items.Add(Item(post, local, accountList));
            }
        }

        RangeTitle = IsWeek
            ? first.ToString("d MMM", text.Culture) + " – " + first.AddDays(6).ToString("d MMM yyyy", text.Culture)
            : Anchor.ToString("MMMM yyyy", text.Culture);
    }

    /// <summary>
    /// Moves a post to another day keeping its time of day. Scheduled posts and drafts are updated; missed or blocked posts are scheduled
    /// again; sent, stopped or canceled posts do not move.
    /// </summary>
    public async Task<bool> MoveAsync(Guid postId, DateTime day)
    {
        try
        {
            if (await posts.GetAsync(postId, CancellationToken.None).ConfigureAwait(true) is not { } post)
            {
                return false;
            }

            if (!CanMove(post.Status))
            {
                Status = text["calendar.cannot_move"];
                return false;
            }

            var local = LocalOf(post);
            var at = ToUtc(day.Date + local.TimeOfDay);
            if (at <= clock.GetUtcNow())
            {
                Status = text["calendar.past"];
                return false;
            }

            if (post.Status is PostStatus.Missed or PostStatus.Blocked)
            {
                await scheduler.RescheduleAsync(post.Id, at, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                post.ScheduledAtUtc = at;
                post.UpdatedAtUtc = clock.GetUtcNow();

                // Saved only if the scheduler has not started sending it since it was read; sent targets stay as they are.
                if (!await posts.SaveEditAsync(post, [post.Status], CancellationToken.None).ConfigureAwait(true))
                {
                    logger.LogWarning("Post {PostId} changed while it was being moved; not moved", post.Id);
                    Status = text["calendar.cannot_move"];
                    await LoadAsync().ConfigureAwait(true);
                    return false;
                }

                scheduler.Wake();
            }

            logger.LogInformation("Post {PostId} moved to {Day:yyyy-MM-dd}", post.Id, day);
            Status = text.Format("calendar.moved", TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).ToString("g", text.Culture));
            await LoadAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            Report(ex);
            return false;
        }
    }

    /// <summary>Shows and logs a failure of the view (drag and drop).</summary>
    public void Report(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        logger.LogError(ex, "Calendar action failed");
        Status = text.Format("calendar.failed", ex.Message);
    }

    /// <summary>Opens the post in the compose screen.</summary>
    [RelayCommand]
    public async Task OpenAsync(CalendarItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Post.Status is not (PostStatus.Draft or PostStatus.Scheduled))
        {
            Status = text["calendar.read_only"];
            return;
        }

        await compose.EditAsync(item.Post).ConfigureAwait(true);
        RequestNavigation(compose);
    }

    /// <summary>Previous month or week.</summary>
    [RelayCommand]
    private Task PreviousAsync()
    {
        Anchor = IsWeek ? Anchor.AddDays(-7) : Anchor.AddMonths(-1);
        return LoadAsync();
    }

    /// <summary>Next month or week.</summary>
    [RelayCommand]
    private Task NextAsync()
    {
        Anchor = IsWeek ? Anchor.AddDays(7) : Anchor.AddMonths(1);
        return LoadAsync();
    }

    /// <summary>Back to today.</summary>
    [RelayCommand]
    private Task TodayAsync()
    {
        Anchor = Today();
        return LoadAsync();
    }

    /// <summary>Month view.</summary>
    [RelayCommand]
    private Task ShowMonthAsync()
    {
        IsWeek = false;
        return LoadAsync();
    }

    /// <summary>Week view.</summary>
    [RelayCommand]
    private Task ShowWeekAsync()
    {
        IsWeek = true;
        return LoadAsync();
    }

    private static bool CanMove(PostStatus status) => status is PostStatus.Scheduled or PostStatus.Draft or PostStatus.Missed or PostStatus.Blocked;

    private static DateTime Monday(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private DateTime Today() => clock.GetLocalNow().Date;

    private DateTimeOffset ToUtc(DateTime local) => new DateTimeOffset(local, clock.LocalTimeZone.GetUtcOffset(local)).ToUniversalTime();

    private DateTime LocalOf(Post post) => TimeZoneInfo.ConvertTime(post.ScheduledAtUtc ?? post.CreatedAtUtc, clock.LocalTimeZone).DateTime;

    private CalendarItemViewModel Item(Post post, DateTime local, Dictionary<Guid, Account> accountList)
    {
        var first = post.Targets.OrderBy(t => t.Order).FirstOrDefault();
        var color = first is not null && accountList.TryGetValue(first.AccountId, out var account) && platforms.Find(account.Platform) is { } platform
            ? platform.Color
            : "#6B7280";
        var summary = (post.Title.Length > 0 ? post.Title : post.Body).ReplaceLineEndings(" ");
        return new CalendarItemViewModel(post, local.ToString("HH:mm", text.Culture), summary.Length > 40 ? summary[..40] + "…" : summary, color,
            text["post." + post.Status.ToString().ToLowerInvariant()], CanMove(post.Status));
    }
}
