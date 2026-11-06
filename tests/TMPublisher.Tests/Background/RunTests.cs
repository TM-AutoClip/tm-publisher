using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Serialization;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.Sending;
using TMPublisher.Runtime.Instance;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Desktop;
using TMPublisher.Tests.Scheduling;

namespace TMPublisher.Tests.Background;

/// <summary>A run the system scheduler starts (<c>--run-due</c>): what it sends, when it does nothing, and the next start it sets.</summary>
public sealed class RunTests
{
    [Fact]
    public async Task ARunSendsOnlyTheDuePostsAndSetsTheNextStart()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var now = h.Clock.GetUtcNow();
        var late = await h.PostAsync(now.AddMinutes(-1), account);
        var due = await h.PostAsync(now, account);
        var later = await h.PostAsync(now.AddHours(2), account);

        (await h.RunAsync()).ShouldBe(RunOutcome.Done);

        (await h.ReloadAsync(late)).Status.ShouldBe(PostStatus.Done);
        (await h.ReloadAsync(due)).Status.ShouldBe(PostStatus.Done);
        (await h.ReloadAsync(later)).Status.ShouldBe(PostStatus.Scheduled);
        platform.Sent.Count.ShouldBe(2);
        h.Notifier.All.Count(n => n.Kind == NotificationKind.Published).ShouldBe(2);
        var plan = h.Trigger.Applied.Last();
        plan.NextUtc.ShouldBe(later.ScheduledAtUtc);
        plan.Safety.ShouldBeTrue();
        plan.Arguments.ShouldBe(["--run-due", "--data-dir", h.Paths.DataDirectory]);

        // A second run finds nothing due, sends nothing and leaves the unchanged entry alone.
        (await h.RunAsync()).ShouldBe(RunOutcome.Done);
        platform.Sent.Count.ShouldBe(2);
        h.Trigger.Applied.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AMissedStartCatchesUpWithinTheGraceAndLaterPostsWaitForTheUser()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var now = h.Clock.GetUtcNow();
        var withinGrace = await h.PostAsync(now - TimeSpan.FromMinutes(3), account);
        var tooLate = await h.PostAsync(now - TimeSpan.FromMinutes(10), account);

        (await h.RunAsync()).ShouldBe(RunOutcome.Done);

        (await h.ReloadAsync(withinGrace)).Status.ShouldBe(PostStatus.Done);
        (await h.ReloadAsync(tooLate)).Status.ShouldBe(PostStatus.Missed);
        platform.Sent.Count.ShouldBe(1);
        h.Notifier.All.ShouldContain(n => n.Title == "notify.missed" || n.Kind == NotificationKind.Info);
    }

    [Fact]
    public async Task TwoRunsNeverOverlap()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow(), account);

        using (var other = InstanceLock.TryAcquire(h.Paths.DataDirectory, InstanceLock.Sender, out _))
        {
            other.ShouldNotBeNull();
            (await h.RunAsync()).ShouldBe(RunOutcome.OtherSender);
            (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Scheduled);
            platform.Sent.ShouldBeEmpty();
        }

        (await h.RunAsync()).ShouldBe(RunOutcome.Done);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Done);
    }

    [Fact]
    public async Task WhileTheWindowIsOpenTheRunEndsAtOnce()
    {
        using var window = new DesktopHarness();
        var vault = window.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.AutomaticProtector ?? vault.PasswordProtector, TestContext.Current.CancellationToken);
        var sender = window.Services.GetRequiredService<WindowSender>();
        await sender.StartAsync(TestContext.Current.CancellationToken);
        sender.IsSending.ShouldBeTrue();

        using var h = new RunHarness(window.Paths.DataDirectory);
        (await h.RunAsync()).ShouldBe(RunOutcome.OtherSender);

        sender.Close();
        sender.IsSending.ShouldBeFalse();
    }

    [Fact]
    public async Task AMasterPasswordRunSendsNothingAndAsksOnceToOpenTheApp()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync(masterPassword: true);
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(-1), account);

        // What the window wrote when it set the start for that post.
        File.WriteAllText(Path.Combine(h.Paths.DataDirectory, "system-trigger.json"), System.Text.Json.JsonSerializer.Serialize(
            new TriggerRecord { State = TriggerState.On, NextPostUtc = post.ScheduledAtUtc }, BackgroundJsonContext.Default.TriggerRecord));

        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);
        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);

        platform.Sent.ShouldBeEmpty();
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Scheduled);
        h.Notifier.All.Count.ShouldBe(1);
        h.Notifier.All[0].Title.ShouldBe("Open TM Publisher to send");
    }

    [Fact]
    public async Task WithoutADatabaseARunDoesNothing()
    {
        using var h = new RunHarness();
        (await h.RunAsync()).ShouldBe(RunOutcome.NoDatabase);
        h.Trigger.Applied.ShouldBeEmpty();
    }
}
