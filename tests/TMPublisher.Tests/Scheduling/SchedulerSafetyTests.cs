using Shouldly;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Scheduling;

namespace TMPublisher.Tests.Scheduling;

/// <summary>What the scheduler does when the database fails, an old version runs or a stop is asked for while posts are due.</summary>
public sealed class SchedulerSafetyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnErrorAfterTheClaimLeavesThePostStoppedForTheUserNotPublishing()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(1), a);
        await h.Scheduler.StartAsync(Ct);

        h.Failing.FailGets = 1;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        var stopped = await h.WaitForAsync(post, PostStatus.Stopped);

        stopped.Status.ShouldBe(PostStatus.Stopped);
        platform.Sent.ShouldBeEmpty();
        h.Notifier.All.ShouldContain(n => n.Kind == NotificationKind.Failed);
    }

    [Fact]
    public async Task ADatabaseErrorIsTriedAgainAfterAGrowingWaitAndShownOnce()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(1), a);
        await h.Scheduler.StartAsync(Ct);

        h.Failing.FailClaims = true;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await UntilAsync(() => h.Scheduler.NextWakeUtc == h.Clock.GetUtcNow() + TimeSpan.FromMinutes(1));
        h.Failing.Claims.ShouldBe(1);

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await UntilAsync(() => h.Scheduler.NextWakeUtc == h.Clock.GetUtcNow() + TimeSpan.FromMinutes(2));
        h.Failing.Claims.ShouldBe(2);
        h.Notifier.All.Count(n => n.Kind == NotificationKind.Failed).ShouldBe(1);

        h.Failing.FailClaims = false;
        h.Clock.Advance(TimeSpan.FromMinutes(2));
        (await h.WaitForAsync(post, PostStatus.Done)).Status.ShouldBe(PostStatus.Done);
    }

    [Fact]
    public async Task ATimerIsLeftEvenWhenTheScheduleCannotBeRead()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(1), a);
        await h.Scheduler.StartAsync(Ct);

        h.Failing.FailNext = true;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await UntilAsync(() => h.Scheduler.NextWakeUtc == h.Clock.GetUtcNow() + TimeSpan.FromMinutes(1));

        h.Failing.FailNext = false;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        (await h.WaitForAsync(post, PostStatus.Done)).Status.ShouldBe(PostStatus.Done);
    }

    [Fact]
    public void TheWaitAfterErrorsGrowsUpToTheSafetyInterval()
    {
        PostScheduler.Backoff(1).ShouldBe(TimeSpan.FromMinutes(1));
        PostScheduler.Backoff(2).ShouldBe(TimeSpan.FromMinutes(2));
        PostScheduler.Backoff(4).ShouldBe(TimeSpan.FromMinutes(8));
        PostScheduler.Backoff(5).ShouldBe(SchedulerLimits.SafetyInterval);
        PostScheduler.Backoff(50).ShouldBe(SchedulerLimits.SafetyInterval);
    }

    [Fact]
    public async Task NothingIsClaimedWhileAnOldVersionRuns()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(1), a);
        await h.Scheduler.StartAsync(Ct);

        h.OldVersion.Runs = true;
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await UntilAsync(() => h.Scheduler.NextWakeUtc == h.Clock.GetUtcNow() + SchedulerLimits.ClaimRetry);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Scheduled);
        h.Failing.Claims.ShouldBe(0);
        h.Notifier.All.ShouldContain(n => n.Kind == NotificationKind.Failed && n.Message == "scheduler.old_version");

        h.OldVersion.Runs = false;
        h.Clock.Advance(SchedulerLimits.ClaimRetry);
        (await h.WaitForAsync(post, PostStatus.Done)).Status.ShouldBe(PostStatus.Done);
        platform.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AStopClaimsNoFurtherPostAndAStopWhenIdleIsRefusedWhileSending()
    {
        var platform = new ScriptedPlatform();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        platform.Script.Enqueue(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30), Ct);
            return new PublishResult("id-1", "https://example.com/1", TargetStatus.Published);
        });
        using var h = await SchedulerHarness.CreateAsync(platform);
        var a = await h.AccountAsync(platform.Code, "A");
        var first = await h.PostAsync(h.Clock.GetUtcNow().AddSeconds(-2), a);
        var second = await h.PostAsync(h.Clock.GetUtcNow().AddSeconds(-1), a);

        var start = Task.Run(() => h.Scheduler.StartAsync(Ct), Ct);
        entered.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();

        // The window's Stop / Restore while the first post is being sent: "busy", nothing stopped.
        (await h.Scheduler.StopAsync(onlyIfIdle: true, Ct)).ShouldBeFalse();

        // Quitting (system shutdown, tray Quit): the post being sent is finished, the second one is not claimed any more.
        var stop = h.Scheduler.StopAsync(onlyIfIdle: false, Ct);
        await Task.Delay(100, Ct);
        release.Set();
        (await stop).ShouldBeTrue();
        await start;

        (await h.ReloadAsync(first)).Status.ShouldBe(PostStatus.Done);
        (await h.ReloadAsync(second)).Status.ShouldBe(PostStatus.Scheduled);
        platform.Sent.Count.ShouldBe(1);
        h.Scheduler.NextWakeUtc.ShouldBeNull();
    }

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(25, Ct);
        }

        done().ShouldBeTrue();
    }
}
