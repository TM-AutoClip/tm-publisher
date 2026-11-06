using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Tests.Scheduling;

namespace TMPublisher.Tests.Background;

/// <summary>The next start the system scheduler gets, and the task / unit / autostart definitions written for it (no real scheduler).</summary>
public sealed class TriggerTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 6, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheNextStartIsTheEarliestOfThePostsAndTheProcessingChecks()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        var now = h.Clock.GetUtcNow();
        await h.PostAsync(now.AddHours(2), account);
        var armer = h.Data.GetRequiredService<TriggerArmer>();

        (await armer.ArmAsync(null, CancellationToken.None)).NextRunUtc.ShouldBe(now.AddHours(2));

        // A video sent two minutes ago is still processing: its second check (3 minutes after sending) comes first.
        var video = await h.PostAsync(now.AddMinutes(-2), account);
        video.Status = PostStatus.Done;
        video.Targets[0].Status = TargetStatus.Processing;
        video.Targets[0].ExternalId = "v1";
        video.Targets[0].PublishedAtUtc = now.AddMinutes(-2);
        await h.Data.GetRequiredService<IPostStore>().SaveAsync(video, CancellationToken.None);
        (await armer.ArmAsync(null, CancellationToken.None)).NextRunUtc.ShouldBe(now.AddMinutes(1));

        // A retry comes even sooner; nothing in the past is set (at least a minute ahead).
        (await armer.ArmAsync(TimeSpan.FromSeconds(10), CancellationToken.None)).NextRunUtc.ShouldBe(now + TriggerLimits.SoonestStart);
        // That is the same minute as the processing check: the unchanged entry is not written again.
        h.Trigger.Applied.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PausedLeavesOnlyTheSignInStartAndOffRemovesTheEntry()
    {
        var platform = new ScriptedPlatform();
        using var h = new RunHarness(null, platform);
        await h.CreateAsync();
        var account = await h.AccountAsync(platform.Code, "A");
        await h.PostAsync(h.Clock.GetUtcNow().AddHours(1), account);
        var armer = h.Data.GetRequiredService<TriggerArmer>();

        h.Data.GetRequiredService<PauseSwitch>().Set(true);
        var paused = await armer.ArmAsync(null, CancellationToken.None);
        paused.State.ShouldBe(TriggerState.On);
        paused.NextRunUtc.ShouldBeNull();
        h.Trigger.Applied.Last().Safety.ShouldBeFalse();

        h.Data.GetRequiredService<PauseSwitch>().Set(false);
        var store = h.Data.GetRequiredService<ISettingsStore>();
        var settings = await store.GetAsync(CancellationToken.None);
        settings.StartWithSystem = false;
        await store.SaveAsync(settings, CancellationToken.None);
        var off = await armer.ArmAsync(null, CancellationToken.None);
        off.State.ShouldBe(TriggerState.Off);
        h.Trigger.Removed.ShouldBe([armer.Name]);
    }

    [Fact]
    public void TheWindowsTaskRunsForTheUserWithoutAdministratorRights()
    {
        var plan = new TriggerPlan("TM Publisher 1A2B3C4D", @"C:\Program Files\TM Publisher\TMPublisher.exe", ["--run-due", "--data-dir", @"D:\my data\"],
            At, Safety: true, WakeToRun: false);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var xml = WindowsTaskXml.Build(plan, "S-1-5-21-1");
        xml.ShouldStartWith("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
        var task = XDocument.Parse(xml).Root!;

        var times = task.Descendants(ns + "TimeTrigger").Select(t => t.Element(ns + "StartBoundary")!.Value).ToList();
        times.ShouldBe(["2026-11-06T18:30:00Z", "2026-11-06T19:00:00Z", "2026-11-06T19:30:00Z", "2026-11-06T20:30:00Z"]);
        task.Descendants(ns + "Repetition").ShouldBeEmpty();
        task.Descendants(ns + "LogonTrigger").Single().Element(ns + "UserId")!.Value.ShouldBe("S-1-5-21-1");
        var principal = task.Descendants(ns + "Principal").Single();
        principal.Element(ns + "LogonType")!.Value.ShouldBe("InteractiveToken");
        principal.Element(ns + "RunLevel")!.Value.ShouldBe("LeastPrivilege");
        var settings = task.Element(ns + "Settings")!;
        settings.Element(ns + "StartWhenAvailable")!.Value.ShouldBe("true");
        settings.Element(ns + "WakeToRun")!.Value.ShouldBe("false");
        settings.Element(ns + "MultipleInstancesPolicy")!.Value.ShouldBe("IgnoreNew");
        settings.Element(ns + "ExecutionTimeLimit")!.Value.ShouldBe("PT1H");
        settings.Element(ns + "DisallowStartIfOnBatteries")!.Value.ShouldBe("false");
        var exec = task.Descendants(ns + "Exec").Single();
        exec.Element(ns + "Command")!.Value.ShouldBe(@"C:\Program Files\TM Publisher\TMPublisher.exe");
        exec.Element(ns + "Arguments")!.Value.ShouldBe(@"--run-due --data-dir ""D:\my data\\""");

        // Nothing scheduled: only the sign-in start; the wake option is passed on.
        var idle = XDocument.Parse(WindowsTaskXml.Build(plan with { NextUtc = null, Safety = false, WakeToRun = true }, "S-1-5-21-1")).Root!;
        idle.Descendants(ns + "TimeTrigger").ShouldBeEmpty();
        idle.Descendants(ns + "LogonTrigger").Count().ShouldBe(1);
        idle.Element(ns + "Settings")!.Element(ns + "WakeToRun")!.Value.ShouldBe("true");
    }

    [Fact]
    public void TheSystemdTimerStartsAtThePostCatchesUpAndHasSafetyStarts()
    {
        var plan = new TriggerPlan("tm-publisher-1a2b3c4d", "/opt/tm publisher/tm-publisher", ["--run-due", "--data-dir", "/home/u/my $data 100%"],
            At, Safety: true, WakeToRun: false);
        var service = SystemdUnits.Service(plan).Split('\n');
        service.ShouldContain("Type=oneshot");
        service.ShouldContain(@"ExecStart=""/opt/tm publisher/tm-publisher"" --run-due --data-dir ""/home/u/my $$data 100%%""");
        service.ShouldContain("TimeoutStartSec=60min");
        var timer = SystemdUnits.Timer(plan)!.Split('\n');
        timer.Where(l => l.StartsWith("OnCalendar=", StringComparison.Ordinal)).ShouldBe(
            ["OnCalendar=2026-11-06 18:30:00 UTC", "OnCalendar=2026-11-06 19:00:00 UTC", "OnCalendar=2026-11-06 19:30:00 UTC", "OnCalendar=2026-11-06 20:30:00 UTC"]);
        timer.ShouldContain("Persistent=true");
        timer.ShouldContain("Unit=tm-publisher-1a2b3c4d.service");
        timer.ShouldContain("WantedBy=timers.target");

        // No time counted from the boot, the sign-in or the last start: such a timer fires at once each time it is written again.
        timer.ShouldNotContain(l => l.StartsWith("OnStartupSec", StringComparison.Ordinal) || l.StartsWith("OnBootSec", StringComparison.Ordinal)
            || l.StartsWith("OnUnitActiveSec", StringComparison.Ordinal));
        SystemdUnits.Timer(plan with { NextUtc = null, Safety = false }).ShouldBeNull();
    }

    [Fact]
    public void TheAutostartFallbackRunsAtSignIn()
    {
        var plan = new TriggerPlan("tm-publisher-1a2b3c4d", "/opt/tm/tm-publisher", ["--run-due", "--data-dir", "/home/u/my $data 100%"], At, true, false);
        var lines = XdgAutostartEntry.Text(plan).Split('\n');
        lines.ShouldContain(@"Exec=""/opt/tm/tm-publisher"" --run-due --data-dir ""/home/u/my \\$data 100%%""");
        lines.ShouldContain("NoDisplay=true");
    }

    [Fact]
    public void OnlyTheOldEntriesOfTheSameDataFolderAreLeftovers()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "own " + Guid.NewGuid().ToString("N"));
        try
        {
            var own = new AppPaths(folder);
            LegacyEntries.IsLeftoverOf(own, "\"C:\\x\\TMPublisherScheduler.exe\" --data-dir \"" + folder + "\"").ShouldBeTrue();
            LegacyEntries.IsLeftoverOf(own, "\"C:\\x\\TMPublisher.exe\" --tray --data-dir \"" + folder + "\"").ShouldBeTrue();
            LegacyEntries.IsLeftoverOf(own, "\"C:\\x\\TMPublisherScheduler.exe\"").ShouldBeFalse();
            LegacyEntries.IsLeftoverOf(own, "\"C:\\x\\TMPublisherScheduler.exe\" --data-dir \"D:\\other\"").ShouldBeFalse();
            LegacyEntries.IsLeftoverOf(own, "\"C:\\x\\TMPublisher.exe\" --run-due --data-dir \"" + folder + "\"").ShouldBeFalse();
            TriggerNames.RunArguments(own).ShouldBe(["--run-due", "--data-dir", folder]);
            TriggerNames.For(own).ShouldBe(TriggerNames.For(new AppPaths(folder)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ProcessingChecksFollowTheWindowsIntervalsFromTheSendingTime()
    {
        var times = ProcessingWatcher.CheckTimes(At);
        times.Select(t => (t - At).TotalMinutes).ShouldBe([1, 3, 8, 18, 38, 68]);
    }
}
