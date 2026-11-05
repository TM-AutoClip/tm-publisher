using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Quotas;
using TMPublisher.Scheduling;
using TMPublisher.Scheduling.Accounts;

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
        h.Scheduler.NextPostUtc.ShouldBe(post.ScheduledAtUtc);
        h.Scheduler.NextWakeUtc.ShouldBe(h.Clock.GetUtcNow() + SchedulerLimits.SafetyInterval);
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
    public async Task TwoRefreshesAtOnceUseTheRotatedTokenOnce()
    {
        var platform = new ScriptedPlatform();
        var refresher = new RotatingRefresher(platform.Code);
        platform.Refresher = refresher;
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.Credentials.SaveAsync(account.Id, Credentials.Empty.With((CredentialKeys.AccessToken, "t-A"), (CredentialKeys.RefreshToken, "r-0")), Ct);

        var both = await Task.WhenAll(h.Renewer.GetFreshAsync(account, Ct), h.Renewer.GetFreshAsync(account, Ct));

        refresher.Calls.ShouldBe(1);
        both.Select(c => c[CredentialKeys.AccessToken]).ShouldBe(["fresh-1", "fresh-1"]);
        (await h.Accounts.GetAsync(account.Id, Ct))!.State.ShouldBe(AccountState.Active);

        // A renew after a 401 with a token another task already replaced uses the stored one instead of refreshing again.
        var renew = h.Renewer.RenewFor(account, Credentials.Empty.With((CredentialKeys.AccessToken, "t-A")))!;
        (await renew(Ct))[CredentialKeys.AccessToken].ShouldBe("fresh-1");
        refresher.Calls.ShouldBe(1);
        (await renew(Ct))[CredentialKeys.AccessToken].ShouldBe("fresh-2");
    }

    [Fact]
    public async Task AfterWakingTheDuePostWaitsForTheNetworkAndIsNotMissed()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(-3), account);
        h.Scheduler.NetworkAvailable = () => false;

        // The Wi-Fi never comes back: after the bounded wait the post is sent anyway, and its lateness counts from the moment of waking.
        var resumedAt = h.Clock.GetUtcNow();
        var run = h.Scheduler.ResumeAsync(resumedAt);
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        await Task.Delay(20, Ct);
        platform.Sent.ShouldBeEmpty();
        for (var i = 0; i < 1000 && !run.IsCompleted; i++)
        {
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(2, Ct);
        }

        await run;
        (h.Clock.GetUtcNow() - post.ScheduledAtUtc!.Value).ShouldBeGreaterThan(SchedulerLimits.LateTolerance);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Done);
        platform.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReloadArmsThePostsOfReplacedData()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.Scheduler.StartAsync(Ct);
        h.Scheduler.NextPostUtc.ShouldBeNull();

        // As after a restore: the data now holds a scheduled post and one left "publishing" when the backup was taken.
        var later = await h.PostAsync(h.Clock.GetUtcNow().AddHours(1), account);
        var interrupted = await h.PostAsync(h.Clock.GetUtcNow().AddHours(-1), account);
        interrupted.Status = PostStatus.Publishing;
        interrupted.Targets[0].Status = TargetStatus.Publishing;
        await h.Posts.SaveAsync(interrupted, Ct);

        await h.Scheduler.ReloadAsync(Ct);

        h.Scheduler.NextPostUtc.ShouldBe(later.ScheduledAtUtc);
        (await h.ReloadAsync(interrupted)).Status.ShouldBe(PostStatus.Stopped);
    }

    [Fact]
    public async Task LongWaitsAreCutIntoSafetyIntervals()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.PostAsync(h.Clock.GetUtcNow().AddDays(3), account);
        await h.Scheduler.StartAsync(Ct);
        h.Scheduler.NextWakeUtc.ShouldBe(h.Clock.GetUtcNow() + SchedulerLimits.SafetyInterval);
    }

    [Fact]
    public async Task WakeReArmsForAnEarlierPost()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        await h.PostAsync(h.Clock.GetUtcNow().AddHours(5), account);
        await h.Scheduler.StartAsync(Ct);
        var earlier = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(10), account);
        await h.Scheduler.RunDueAsync(Ct);
        h.Scheduler.NextWakeUtc.ShouldBe(earlier.ScheduledAtUtc);
        h.Scheduler.NextPostUtc.ShouldBe(earlier.ScheduledAtUtc);
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

        (await h.Decisions.SendNowAsync(missed.Id, Ct)).ShouldBeTrue();
        await h.Scheduler.RunDueAsync(Ct);
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

        (await h.Decisions.SkipFailedAsync(post.Id, Ct)).ShouldBeTrue();
        await h.Scheduler.RunDueAsync(Ct);
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
        (await h.Decisions.RetryAsync(post.Id, Ct)).ShouldBeTrue();
        await h.Scheduler.RunDueAsync(Ct);
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

        (await h.Decisions.CancelAsync(post.Id, Ct)).ShouldBeTrue();
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
    public async Task APostMovedAfterItWasReadIsNotClaimedWithItsOldTime()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), account);
        var oldTime = post.ScheduledAtUtc;

        // The window moves the post while the scheduler holds the old reading.
        var edited = await h.ReloadAsync(post);
        edited.ScheduledAtUtc = h.Clock.GetUtcNow().AddDays(1);
        (await h.Posts.SaveEditAsync(edited, PostEditing.EditableStates, Ct)).ShouldBeTrue();

        (await h.Posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, oldTime, PostStatus.Publishing), Ct)).ShouldBeFalse();
        (await h.Posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, edited.ScheduledAtUtc, PostStatus.Publishing), Ct)).ShouldBeTrue();
        (await h.Posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, edited.ScheduledAtUtc, PostStatus.Publishing), Ct)).ShouldBeFalse();
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Publishing);
    }

    [Fact]
    public async Task OnlyAClaimedPostIsSentAndADecisionDoesNotTouchIt()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), account);

        await Should.ThrowAsync<InvalidOperationException>(() => h.Sender.SendAsync(post, Ct));
        platform.Sent.ShouldBeEmpty();

        (await h.Posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, post.ScheduledAtUtc, PostStatus.Publishing), Ct)).ShouldBeTrue();
        (await h.Decisions.CancelAsync(post.Id, Ct)).ShouldBeFalse();
        (await h.Decisions.SendNowAsync(post.Id, Ct)).ShouldBeFalse();
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Publishing);

        // A second scheduler run finds nothing to claim: the post goes out once.
        await h.Scheduler.StartAsync(Ct);
        await h.Scheduler.RunDueAsync(Ct);
        platform.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task APausedSchedulerSendsNothingAndLatePostsWaitAfterResume()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), account);

        await h.Scheduler.StartAsync(Ct, paused: true);
        h.Scheduler.IsPaused.ShouldBeTrue();
        h.Scheduler.NextWakeUtc.ShouldBeNull();
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Scheduled);

        h.Clock.Advance(TimeSpan.FromMinutes(10));
        h.Scheduler.Resume();
        (await h.WaitForAsync(post, PostStatus.Missed)).Status.ShouldBe(PostStatus.Missed);
        platform.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheRefreshLockMakesTheOtherProcessWait()
    {
        var platform = new ScriptedPlatform();
        using var h = await SchedulerHarness.CreateAsync(platform);
        var account = Guid.NewGuid();

        // Another gate on the same data folder stands for the other process (window or scheduler).
        var other = new FileRefreshGate(h.Paths, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileRefreshGate>.Instance);
        var held = await h.Gate.EnterAsync(account, Ct);
        var waiting = other.EnterAsync(account, Ct);
        await Task.Delay(500, Ct);
        waiting.IsCompleted.ShouldBeFalse();

        await held.DisposeAsync();
        await using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await using var third = await other.EnterAsync(Guid.NewGuid(), Ct);
        third.ShouldNotBeNull();
    }

    [Fact]
    public void PacificDayStartsAtPacificMidnight()
    {
        var start = QuotaChecker.WindowStart(TMPublisher.Core.Platforms.QuotaWindow.PacificDay, new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        start.ShouldBe(new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero));
    }
}
