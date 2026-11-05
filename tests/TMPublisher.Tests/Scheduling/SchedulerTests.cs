using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Quotas;
using TMPublisher.Scheduling;

namespace TMPublisher.Tests.Scheduling;

public sealed class SchedulerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OneTimerFiresAtThePostTimeAndSendsExactlyTheStoredText()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddHours(1), account);

        await h.Scheduler.StartAsync(Ct);
        h.Scheduler.NextWakeUtc.ShouldBe(post.ScheduledAtUtc);
        platform.Sent.ShouldBeEmpty();

        h.Clock.Advance(TimeSpan.FromHours(1));
        var done = await h.WaitForAsync(post, PostStatus.Done);
        done.Status.ShouldBe(PostStatus.Done);
        platform.Sent.Single().Text.ShouldBe("Hello A");
        done.Targets.Single().Status.ShouldBe(TargetStatus.Published);
        done.Targets.Single().Url.ShouldBe("https://example.com/1");
        h.Notifier.All.ShouldContain(n => n.Kind == NotificationKind.Published);
        (await h.Log.ListAsync(account.Id, h.Clock.GetUtcNow().AddDays(-1), Ct)).Single().Success.ShouldBeTrue();
    }

    [Fact]
    public async Task LongWaitsAreCutInto24HourPieces()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.PostAsync(h.Clock.GetUtcNow().AddDays(3), account);
        await h.Scheduler.StartAsync(Ct);
        h.Scheduler.NextWakeUtc.ShouldBe(h.Clock.GetUtcNow().AddHours(24));
    }

    [Fact]
    public async Task WakeReArmsForAnEarlierPost()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.PostAsync(h.Clock.GetUtcNow().AddHours(5), account);
        await h.Scheduler.StartAsync(Ct);
        var earlier = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(30), account);
        await h.Scheduler.RunDueAsync(Ct);
        h.Scheduler.NextWakeUtc.ShouldBe(earlier.ScheduledAtUtc);
    }

    [Fact]
    public async Task LatePostsWithinFiveMinutesAreSentAndOlderOnesWait()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var slightly = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(-3), account);
        var missed = await h.PostAsync(h.Clock.GetUtcNow().AddHours(-2), account);

        await h.Scheduler.StartAsync(Ct);
        (await h.ReloadAsync(slightly)).Status.ShouldBe(PostStatus.Done);
        (await h.ReloadAsync(missed)).Status.ShouldBe(PostStatus.Missed);
        platform.Sent.Count.ShouldBe(1);
        h.Notifier.All.ShouldContain(n => n.Title == "notify.missed");

        await h.Scheduler.SendNowAsync(missed.Id, Ct);
        (await h.ReloadAsync(missed)).Status.ShouldBe(PostStatus.Done);
        platform.Sent.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AFailedTargetStopsTheRestUntilTheUserDecides()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var b = await h.AccountAsync(platform.Code, "B");
        var c = await h.AccountAsync(platform.Code, "C");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), a, b, c);
        platform.Script.Enqueue(_ => new PublishResult("1", "", TargetStatus.Published));
        platform.Script.Enqueue(_ => throw new PlatformRejectedException("file refused"));

        await h.Scheduler.StartAsync(Ct);
        var stopped = await h.ReloadAsync(post);
        stopped.Status.ShouldBe(PostStatus.Stopped);
        stopped.Targets.Select(t => t.Status).ShouldBe([TargetStatus.Published, TargetStatus.Failed, TargetStatus.Pending]);
        stopped.Targets[1].Message.ShouldBe("file refused");
        h.Notifier.All.ShouldContain(n => n.Kind == NotificationKind.Failed);

        await h.Scheduler.SkipFailedAsync(post.Id, Ct);
        var done = await h.ReloadAsync(post);
        done.Status.ShouldBe(PostStatus.Done);
        done.Targets.Select(t => t.Status).ShouldBe([TargetStatus.Published, TargetStatus.Skipped, TargetStatus.Published]);
        platform.Sent.Select(t => t.AccountId).ShouldBe([a.Id, b.Id, c.Id]);
    }

    [Fact]
    public async Task RetrySendsTheFailedTargetAgain()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), a);
        platform.Script.Enqueue(_ => throw new HttpRequestException("network down"));

        await h.Scheduler.StartAsync(Ct);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Stopped);
        await h.Scheduler.RetryAsync(post.Id, Ct);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Done);
        platform.Sent.Count.ShouldBe(2);
    }

    [Fact]
    public async Task APostThatCannotBeSentIsBlockedBeforeAnyRequest()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var ok = await h.AccountAsync(platform.Code, "A");
        var expired = await h.AccountAsync(platform.Code, "B", AccountState.NeedsReconnect);
        var post = await h.PostAsync(h.Clock.GetUtcNow(), ok, expired);

        await h.Scheduler.StartAsync(Ct);
        var blocked = await h.ReloadAsync(post);
        blocked.Status.ShouldBe(PostStatus.Blocked);
        platform.Sent.ShouldBeEmpty();
        blocked.Targets[1].Message.ShouldStartWith("problem.reconnect");

        await h.Scheduler.CancelAsync(post.Id, Ct);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Canceled);
    }

    [Fact]
    public async Task TheLocalQuotaBlocksAPostOverTheLimit()
    {
        var platform = new ScriptedPlatform(dailyLimit: 1);
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        await h.Log.AddAsync(new PublishLogEntry(a.Id, platform.Code, Guid.NewGuid(), h.Clock.GetUtcNow().AddHours(-2), true), Ct);
        var post = await h.PostAsync(h.Clock.GetUtcNow(), a);

        await h.Scheduler.StartAsync(Ct);
        var blocked = await h.ReloadAsync(post);
        blocked.Status.ShouldBe(PostStatus.Blocked);
        blocked.Targets.Single().Message.ShouldStartWith("problem.quota");
        platform.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task APostInterruptedByClosingTheProgramWaitsForTheUser()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), a);
        post.Status = PostStatus.Publishing;
        post.Targets[0].Status = TargetStatus.Publishing;
        await h.Posts.SaveAsync(post, Ct);

        await h.Scheduler.StartAsync(Ct);
        var stopped = await h.ReloadAsync(post);
        stopped.Status.ShouldBe(PostStatus.Stopped);
        stopped.Targets.Single().Status.ShouldBe(TargetStatus.Failed);
        platform.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessingUploadsAreCheckedAgainUntilPublished()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), a);
        platform.Script.Enqueue(_ => new PublishResult("vid", "https://example.com/v", TargetStatus.Processing));

        await h.Scheduler.StartAsync(Ct);
        (await h.ReloadAsync(post)).Targets.Single().Status.ShouldBe(TargetStatus.Processing);
        platform.Processing = _ => new ProcessingCheck(TargetStatus.Published, "https://example.com/watch");
        h.Clock.Advance(SchedulerLimits.ProcessingChecks[0]);
        for (var i = 0; i < 200 && (await h.ReloadAsync(post)).Targets.Single().Status == TargetStatus.Processing; i++)
        {
            await Task.Delay(25, Ct);
        }

        var target = (await h.ReloadAsync(post)).Targets.Single();
        target.Status.ShouldBe(TargetStatus.Published);
        target.Url.ShouldBe("https://example.com/watch");
    }

    [Fact]
    public void PacificDayStartsAtPacificMidnight()
    {
        var start = QuotaChecker.WindowStart(TMPublisher.Core.Platforms.QuotaWindow.PacificDay, new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        start.ShouldBe(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
    }
}
