using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Desktop;

public sealed class CalendarTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    // Wednesday 11 March 2026, 09:00 UTC; the fake clock's local zone is UTC.
    private static readonly DateTimeOffset Now = new(2026, 3, 11, 9, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public async Task MonthViewStartsOnMondayAndPlacesPostsOnTheirDay()
    {
        using var h = await OpenAsync();
        var account = await AccountAsync(h);
        await PostAsync(h, Now.AddDays(2).AddHours(5), PostStatus.Scheduled, account);
        var calendar = Page<CalendarViewModel>(h);
        var view = new CalendarView { DataContext = calendar };
        await calendar.ActivateAsync();

        calendar.Days.Count.ShouldBe(42);
        calendar.Days[0].Date.ShouldBe(new DateTime(2026, 2, 23));
        calendar.Days[0].InRange.ShouldBeFalse();
        calendar.Days.Single(d => d.IsToday).Date.ShouldBe(new DateTime(2026, 3, 11));
        var item = calendar.Days.Single(d => d.Date == new DateTime(2026, 3, 13)).Items.Single();
        item.Time.ShouldBe("14:00");
        item.Color.ShouldBe(h.Services.GetRequiredService<IPlatformDirectory>().Get(PlatformCodes.Bluesky).Color);
        view.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task WeekViewShowsSevenDaysAndMovesWithTheArrows()
    {
        using var h = await OpenAsync();
        var calendar = Page<CalendarViewModel>(h);
        await calendar.ActivateAsync();
        await calendar.ShowWeekCommand.ExecuteAsync(null);
        calendar.Days.Count.ShouldBe(7);
        calendar.Days[0].Date.ShouldBe(new DateTime(2026, 3, 9));
        await calendar.NextCommand.ExecuteAsync(null);
        calendar.Days[0].Date.ShouldBe(new DateTime(2026, 3, 16));
    }

    [AvaloniaFact]
    public async Task DraggingKeepsTheTimeOfDayAndRefusesThePast()
    {
        using var h = await OpenAsync();
        var account = await AccountAsync(h);
        var post = await PostAsync(h, Now.AddDays(1).AddHours(3), PostStatus.Scheduled, account);
        var calendar = Page<CalendarViewModel>(h);
        await calendar.ActivateAsync();

        (await calendar.MoveAsync(post.Id, new DateTime(2026, 3, 20))).ShouldBeTrue();
        var moved = await h.Services.GetRequiredService<IPostStore>().GetAsync(post.Id, None);
        moved!.ScheduledAtUtc.ShouldBe(new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
        calendar.Days.Single(d => d.Date == new DateTime(2026, 3, 20)).Items.ShouldHaveSingleItem();

        (await calendar.MoveAsync(post.Id, new DateTime(2026, 3, 1))).ShouldBeFalse();
        calendar.Status.ShouldNotBeEmpty();
    }

    [AvaloniaFact]
    public async Task SentPostsDoNotMove()
    {
        using var h = await OpenAsync();
        var account = await AccountAsync(h);
        var post = await PostAsync(h, Now.AddHours(-3), PostStatus.Done, account);
        var calendar = Page<CalendarViewModel>(h);
        await calendar.ActivateAsync();
        (await calendar.MoveAsync(post.Id, new DateTime(2026, 3, 25))).ShouldBeFalse();
        (await h.Services.GetRequiredService<IPostStore>().GetAsync(post.Id, None))!.ScheduledAtUtc.ShouldBe(post.ScheduledAtUtc);
    }

    [AvaloniaFact]
    public async Task OpeningAScheduledPostFillsTheComposeScreen()
    {
        using var h = await OpenAsync();
        var account = await AccountAsync(h);
        var post = await PostAsync(h, Now.AddDays(1), PostStatus.Scheduled, account);
        var calendar = Page<CalendarViewModel>(h);
        var shell = h.Services.GetRequiredService<ShellViewModel>();
        await calendar.ActivateAsync();
        var item = calendar.Days.SelectMany(d => d.Items).Single();
        await calendar.OpenCommand.ExecuteAsync(item);

        var compose = Page<ComposeViewModel>(h);
        compose.IsEditing.ShouldBeTrue();
        compose.Body.ShouldBe("Calendar post");
        compose.Targets.Single().Account.Id.ShouldBe(account.Id);
        for (var i = 0; i < 100 && shell.Current != compose; i++)
        {
            await Task.Delay(10, None);
        }

        shell.Current.ShouldBe(compose);
        _ = post;
    }

    private static T Page<T>(DesktopHarness h) => h.Services.GetServices<PageViewModelBase>().OfType<T>().Single();

    private static async Task<DesktopHarness> OpenAsync()
    {
        var clock = new FakeTimeProvider(Now);
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        var h = new DesktopHarness(s => s.AddSingleton<TimeProvider>(clock));
        h.Services.GetRequiredService<TMPublisher.Desktop.Startup.UiMasterPassword>().Provide("a long master password");
        var vault = h.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.PasswordProtector, None);
        return h;
    }

    private static async Task<Account> AccountAsync(DesktopHarness h)
    {
        var account = new Account { Platform = PlatformCodes.Bluesky, DisplayName = "Me", ExternalId = "me" };
        await h.Services.GetRequiredService<IAccountStore>().SaveAsync(account, None);
        return account;
    }

    private static async Task<Post> PostAsync(DesktopHarness h, DateTimeOffset at, PostStatus status, Account account)
    {
        var post = new Post { Kind = PostKind.Text, Body = "Calendar post", Status = status, ScheduledAtUtc = at, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        post.Targets = [new PostTarget { PostId = post.Id, AccountId = account.Id, Text = "Calendar post" }];
        await h.Services.GetRequiredService<IPostStore>().SaveAsync(post, None);
        return post;
    }
}
