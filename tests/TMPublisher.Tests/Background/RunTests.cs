using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Serialization;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.Sending;
using TMPublisher.Desktop.Startup;
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
    public async Task ARunThatCannotOpenTheDatabaseSetsTheNextStartAfterAGrowingWait()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var post = await h.PostAsync(h.Clock.GetUtcNow().AddHours(2), account);
        (await h.RunAsync()).ShouldBe(RunOutcome.Done);
        h.Trigger.Applied.Last().NextUtc.ShouldBe(post.ScheduledAtUtc);

        // At the post's time the keyring does not answer: the run cannot open the database, so it sets the retry without it.
        h.Clock.Advance(TimeSpan.FromHours(2));
        h.KeyringFails = true;
        (await h.RunAsync()).ShouldBe(RunOutcome.Failed);
        var retry = h.Trigger.Applied.Last();
        retry.NextUtc.ShouldBe(h.Clock.GetUtcNow() + TimeSpan.FromMinutes(1));
        retry.Safety.ShouldBeTrue();
        retry.Arguments.ShouldBe(["--run-due", "--data-dir", h.Paths.DataDirectory]);

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        (await h.RunAsync()).ShouldBe(RunOutcome.Failed);
        h.Trigger.Applied.Last().NextUtc.ShouldBe(h.Clock.GetUtcNow() + TimeSpan.FromMinutes(2));
        h.Notifier.All.Count(n => n.Kind == NotificationKind.Failed).ShouldBe(1);

        // The keyring answers again within the five minutes: the post is sent.
        h.Clock.Advance(TimeSpan.FromMinutes(2));
        h.KeyringFails = false;
        (await h.RunAsync()).ShouldBe(RunOutcome.Done);
        (await h.ReloadAsync(post)).Status.ShouldBe(PostStatus.Done);
    }

    [Fact]
    public async Task ALockedRunSetsTheNextStartToEachPostTimeAndNoticesEachOnce()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync(masterPassword: true);
        var now = h.Clock.GetUtcNow();

        // The window armed a processing check in two minutes; the posts are at +5 h and +7 h.
        File.WriteAllText(Path.Combine(h.Paths.DataDirectory, "system-trigger.json"), System.Text.Json.JsonSerializer.Serialize(
            new TriggerRecord { State = TriggerState.On, NextRunUtc = now.AddMinutes(2), NextPostUtc = now.AddHours(5), PostTimesUtc = [now.AddHours(5), now.AddHours(7)] },
            BackgroundJsonContext.Default.TriggerRecord));

        h.Clock.Advance(TimeSpan.FromMinutes(2));
        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);
        h.Notifier.All.ShouldBeEmpty();
        h.Trigger.Applied.Last().NextUtc.ShouldBe(now.AddHours(5));

        h.Clock.Advance(TimeSpan.FromHours(5) - TimeSpan.FromMinutes(2));
        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);
        h.Notifier.All.Count.ShouldBe(1);
        h.Trigger.Applied.Last().NextUtc.ShouldBe(now.AddHours(7));

        h.Clock.Advance(TimeSpan.FromHours(2));
        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);
        h.Notifier.All.Count.ShouldBe(2);
        h.Trigger.Applied.Last().NextUtc.ShouldBeNull();

        // Nothing new: no notice and the entry is not written again.
        var writes = h.Trigger.Applied.Count;
        (await h.RunAsync()).ShouldBe(RunOutcome.Locked);
        h.Notifier.All.Count.ShouldBe(2);
        h.Trigger.Applied.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task WhileAnOldVersionRunsNothingIsTouchedAndTheUserIsToldOnce()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var sending = await h.PostAsync(h.Clock.GetUtcNow().AddMinutes(-1), account);
        sending.Status = PostStatus.Publishing;
        sending.Targets[0].Status = TargetStatus.Publishing;
        await h.Data.GetRequiredService<IPostStore>().SaveAsync(sending, CancellationToken.None);
        var due = await h.PostAsync(h.Clock.GetUtcNow(), account);
        h.OldVersion.Runs = true;

        (await h.RunAsync()).ShouldBe(RunOutcome.OtherSender);
        h.Trigger.Applied.Last().NextUtc.ShouldBe(h.Clock.GetUtcNow() + TimeSpan.FromMinutes(1));
        (await h.RunAsync()).ShouldBe(RunOutcome.OtherSender);
        h.Trigger.Applied.Last().NextUtc.ShouldBe(h.Clock.GetUtcNow() + TimeSpan.FromMinutes(2));

        h.Notifier.All.Count.ShouldBe(1);
        (await h.ReloadAsync(sending)).Status.ShouldBe(PostStatus.Publishing);
        (await h.ReloadAsync(due)).Status.ShouldBe(PostStatus.Scheduled);
        platform.Sent.ShouldBeEmpty();

        h.OldVersion.Runs = false;
        (await h.RunAsync()).ShouldBe(RunOutcome.Done);
        (await h.ReloadAsync(sending)).Status.ShouldBe(PostStatus.Stopped);
        (await h.ReloadAsync(due)).Status.ShouldBe(PostStatus.Done);
    }

    [Fact]
    public async Task ClosingTheWindowFinishesThePostBeingSent()
    {
        var platform = new ScriptedPlatform { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var window = new DesktopHarness(s =>
        {
            s.RemoveAll<IPlatform>();
            s.AddSingleton<IPlatform>(platform);
        });
        var ct = TestContext.Current.CancellationToken;
        var vault = window.Services.GetRequiredService<Vault>();
        window.Services.GetRequiredService<UiMasterPassword>().Provide("a long master password");
        await vault.CreateAsync(vault.PasswordProtector, ct);
        var account = new Account { Platform = platform.Code, DisplayName = "A", ExternalId = "A", State = AccountState.Active };
        await window.Services.GetRequiredService<IAccountStore>().SaveAsync(account, ct);
        await window.Services.GetRequiredService<ICredentialStore>().SaveAsync(account.Id, Credentials.Empty.With((CredentialKeys.AccessToken, "t-A")), ct);
        var now = DateTimeOffset.UtcNow;
        var post = new Post { Kind = PostKind.Text, Body = "Hello", Status = PostStatus.Scheduled, ScheduledAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now };
        post.Targets = [new PostTarget { PostId = post.Id, AccountId = account.Id, Order = 0, Text = "Hello A" }];
        var posts = window.Services.GetRequiredService<IPostStore>();
        await posts.SaveAsync(post, ct);

        var sender = window.Services.GetRequiredService<WindowSender>();
        sender.QuitWait = TimeSpan.FromMilliseconds(200);
        var start = sender.StartAsync(ct);
        for (var i = 0; i < 400 && platform.Sent.Count == 0; i++)
        {
            await Task.Delay(25, ct);
        }

        platform.Sent.Count.ShouldBe(1);

        // The upload takes longer than the short wait: closing does not cut it off, it waits for it.
        var close = Task.Run(() => sender.Close(), ct);
        await Task.Delay(1000, ct);
        close.IsCompleted.ShouldBeFalse();
        platform.Hold.SetResult();
        await close.WaitAsync(TimeSpan.FromSeconds(30), ct);
        await start.WaitAsync(TimeSpan.FromSeconds(30), ct);

        (await posts.GetAsync(post.Id, ct))!.Status.ShouldBe(PostStatus.Done);
        sender.IsSending.ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutADatabaseARunDoesNothing()
    {
        using var h = new RunHarness();
        (await h.RunAsync()).ShouldBe(RunOutcome.NoDatabase);
        h.Trigger.Applied.ShouldBeEmpty();
    }
}
