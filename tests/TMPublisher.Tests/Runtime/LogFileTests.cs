using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TMPublisher.Runtime.Logging;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Runtime;

/// <summary>The daily log files: names, 7-day retention by the date in the name, day change, size limit, masking, levels, crash handlers.</summary>
public sealed class LogFileTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static string Logs(TempPaths paths)
    {
        Directory.CreateDirectory(paths.LogDirectory);
        return paths.LogDirectory;
    }

    private static void Touch(string folder, string name) => File.WriteAllText(Path.Combine(folder, name), "x");

    [Fact]
    public void RetentionKeepsSixDaysOldDeletesSevenAndEightAndLeavesUnknownFiles()
    {
        using var paths = new TempPaths();
        var logs = Logs(paths);
        Touch(logs, "app-2026-10-31.log");            // 6 days old: kept
        Touch(logs, "scheduler-2026-10-30.log");      // 7 days old: deleted
        Touch(logs, "notify-2026-10-29.log");         // 8 days old: deleted
        Touch(logs, "tmpublisher-scheduler-20261029.log"); // 0.1.3 name, 8 days old: deleted
        Touch(logs, "tmpublisher-20261105.log");      // 0.1.3 name, 1 day old: kept
        Touch(logs, "other-2020-01-01.log");          // not our kind: left alone
        Touch(logs, "notes.txt");                     // not a log: left alone

        var deleted = LogRetention.Clean(logs, Today, (name, ex) => throw new ShouldAssertException(name, ex));

        deleted.Order(StringComparer.Ordinal).ShouldBe(["notify-2026-10-29.log", "scheduler-2026-10-30.log", "tmpublisher-scheduler-20261029.log"]);
        Directory.EnumerateFiles(logs).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .ShouldBe(["app-2026-10-31.log", "notes.txt", "other-2020-01-01.log", "tmpublisher-20261105.log"]);
    }

    [Fact]
    public void ADayChangeWhileRunningOpensANewFileWithAHeaderAndCleansUp()
    {
        using var paths = new TempPaths();
        var logs = Logs(paths);
        Touch(logs, "app-2026-10-31.log"); // 6 days old on the 6th, 7 on the 7th
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 23, 59, 30, TimeSpan.Zero));
        using (var file = new DailyLogFile(logs, LogKinds.App, "TM Publisher", clock))
        {
            file.Write("before midnight");
            File.Exists(Path.Combine(logs, "app-2026-10-31.log")).ShouldBeTrue();
            clock.Advance(TimeSpan.FromMinutes(1));
            file.Write("after midnight");
            file.CurrentFile.ShouldEndWith("app-2026-11-07.log");
        }

        var first = File.ReadAllLines(Path.Combine(logs, "app-2026-11-06.log"));
        var second = File.ReadAllLines(Path.Combine(logs, "app-2026-11-07.log"));
        first[0].ShouldContain("=== TM Publisher ");
        first[0].ShouldContain(LogHeader.Architecture);
        first[^1].ShouldBe("before midnight");
        second[0].ShouldContain("=== TM Publisher ");
        second[^1].ShouldBe("after midnight");
        File.Exists(Path.Combine(logs, "app-2026-10-31.log")).ShouldBeFalse();
    }

    [Fact]
    public void TheDailyLimitWritesOneWarningThenNothingUntilTheNextDay()
    {
        using var paths = new TempPaths();
        var logs = Logs(paths);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        const long limit = 4096;
        using (var file = new DailyLogFile(logs, LogKinds.Scheduler, "TM Publisher scheduler", clock, limit))
        {
            for (var i = 0; i < 100; i++)
            {
                file.Write("line " + i + " " + new string('x', 90));
            }

            clock.Advance(TimeSpan.FromDays(1));
            file.Write("next day");
        }

        var full = File.ReadAllText(Path.Combine(logs, "scheduler-2026-11-06.log"));
        new FileInfo(Path.Combine(logs, "scheduler-2026-11-06.log")).Length.ShouldBeLessThan(limit + 300);
        full.Split("daily limit").Length.ShouldBe(2);
        full.ShouldNotContain("line 99 ");
        File.ReadAllLines(Path.Combine(logs, "scheduler-2026-11-07.log"))[^1].ShouldBe("next day");
    }

    [Fact]
    public void AFileThatCannotBeOpenedIsTriedAgainAndTheLostLinesAreCounted()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // The failure is made with a file opened without sharing, which Windows enforces for every writer.
        }

        using var paths = new TempPaths();
        var logs = Logs(paths);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        var name = Path.Combine(logs, "app-2026-11-06.log");
        var held = new FileStream(name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using (var file = new DailyLogFile(logs, LogKinds.App, "TM Publisher", clock))
        {
            file.Write("lost one");
            clock.Advance(TimeSpan.FromSeconds(30));
            file.Write("lost two");
            held.Dispose();
            clock.Advance(TimeSpan.FromSeconds(31));
            file.Write("written");
        }

        var lines = File.ReadAllLines(name);
        lines[0].ShouldContain("=== TM Publisher ");
        lines[1].ShouldContain("[Warning] TMPublisher.Logging: The log file could not be opened for a while; 2 line(s) were not written");
        lines[^1].ShouldBe("written");
    }

    [Fact]
    public void TwoWritersOfOneKindDoNotOverwriteEachOthersLines()
    {
        using var paths = new TempPaths();
        var logs = Logs(paths);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        var a = new string('a', 60);
        var b = new string('b', 60);
        using (var first = new DailyLogFile(logs, LogKinds.Notify, "TM Publisher", clock))
        using (var second = new DailyLogFile(logs, LogKinds.Notify, "TM Publisher", clock))
        {
            Parallel.Invoke(
                () =>
                {
                    for (var i = 0; i < 400; i++)
                    {
                        first.Write("first " + i.ToString("000", CultureInfo.InvariantCulture) + " " + a);
                    }
                },
                () =>
                {
                    for (var i = 0; i < 400; i++)
                    {
                        second.Write("second " + i.ToString("000", CultureInfo.InvariantCulture) + " " + b);
                    }
                });
        }

        var lines = File.ReadAllLines(Path.Combine(logs, "notify-2026-11-06.log"))
            .Where(l => !l.Contains("=== TM Publisher ", StringComparison.Ordinal)).ToList();
        lines.Count.ShouldBe(800);
        lines.Count(l => l.StartsWith("first ", StringComparison.Ordinal) && l.EndsWith(a, StringComparison.Ordinal) && l.Length == 70).ShouldBe(400);
        lines.Count(l => l.StartsWith("second ", StringComparison.Ordinal) && l.EndsWith(b, StringComparison.Ordinal) && l.Length == 71).ShouldBe(400);
    }

    [Fact]
    public void TheScrubberMasksTheUsersOwnServerNamesOnceRegistered()
    {
        TMPublisher.Core.Security.AccountHosts.Add("social.own-server-0815.example");
        LogScrubber.Scrub("No such host is known. (social.own-server-0815.example:443) for @me@social.own-server-0815.example", null)
            .ShouldBe("No such host is known. ((account server):443) for @***@***");
    }

    [Theory]
    [InlineData(@"Opening C:\Users\u\AppData\Local\TMPublisher\publisher.db", @"Opening ~\AppData\Local\TMPublisher\publisher.db")]
    [InlineData("Opening C:/Users/u/Videos/a.mp4", "Opening ~/Videos/a.mp4")]
    [InlineData("Signed in as name.surname+x@example.co.uk now", "Signed in as ***@*** now")]
    [InlineData("Authorization: Bearer abcdefghijklmnop123", "Authorization: Bearer ***")]
    [InlineData("Authorization: Basic dXNlcjpwYXNzd29yZA==", "Authorization: Basic ***")]
    [InlineData("key sk-ant-abcdefghijklmn used", "key *** used")]
    [InlineData("GET /x?access_token=abc123&a=1", "GET /x?access_token=***&a=1")]
    public void TheScrubberMasksHomeEmailAndTokens(string input, string expected)
    {
        var home = OperatingSystem.IsWindows() ? @"C:\Users\u" : "/home/u";
        var text = OperatingSystem.IsWindows() ? input : input.Replace(@"C:\Users\u", home).Replace("C:/Users/u", home).Replace('\\', '/');
        var want = OperatingSystem.IsWindows() ? expected : expected.Replace('\\', '/');
        LogScrubber.Scrub(text, home).ShouldBe(want);
    }

    [Fact]
    public void OnlyInformationAndAboveAreWrittenMaskedWithTheirCategory()
    {
        using var paths = new TempPaths();
        using (var provider = new FileLoggerProvider(Logs(paths), LogKinds.App))
        {
            var logger = provider.CreateLogger("TMPublisher.Test");
            logger.LogDebug("debug line");
            logger.LogTrace("trace line");
            logger.LogInformation("info for {Mail}", "someone@example.com");
            logger.LogWarning("warning line");
            logger.LogError(new InvalidOperationException("broken"), "error line");
            logger.LogCritical("critical line");
        }

        var text = File.ReadAllText(Directory.GetFiles(paths.LogDirectory, "app-*.log").Single());
        text.ShouldNotContain("debug line");
        text.ShouldNotContain("trace line");
        text.ShouldContain("[Information] TMPublisher.Test: info for ***@***");
        text.ShouldContain("[Warning] TMPublisher.Test: warning line");
        text.ShouldContain("[Error] TMPublisher.Test: error line");
        text.ShouldContain("InvalidOperationException: broken");
        text.ShouldContain("[Critical] TMPublisher.Test: critical line");
    }

    [Fact]
    public void UnhandledAndUnobservedExceptionsAreCritical()
    {
        using var recorder = new RecordingLoggerProvider();
        var logger = recorder.CreateLogger("Crash");
        CrashLogging.Unhandled(logger, new InvalidOperationException("boom"), "process");
        CrashLogging.Unobserved(logger, new AggregateException(new TimeoutException("late")));

        recorder.Entries.Count.ShouldBe(2);
        recorder.Entries.ShouldAllBe(e => e.Level == LogLevel.Critical && e.Exception != null);
        recorder.Entries.First().Text.ShouldContain("InvalidOperationException");
    }
}
