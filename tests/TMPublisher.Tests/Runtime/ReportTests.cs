using System.IO.Compression;
using Shouldly;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Reports;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Runtime;

/// <summary>The logs zip of "Report a problem" and the GitHub issue address.</summary>
public sealed class ReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void TheZipHoldsSystemTxtAndOnlyOurRecentLogsMaskedAgain()
    {
        using var paths = new TempPaths();
        var logs = Directory.CreateDirectory(paths.LogDirectory).FullName;
        var home = OperatingSystem.IsWindows() ? @"C:\Users\u" : "/home/u";
        var sep = Path.DirectorySeparatorChar;
        File.WriteAllText(Path.Combine(logs, "app-2026-11-06.log"), "opened " + home + sep + "Videos" + sep + "a.mp4 for someone@example.com\n");
        File.WriteAllText(Path.Combine(logs, "scheduler-2026-11-01.log"), "token Bearer abcdefghijklmnop\n");
        File.WriteAllText(Path.Combine(logs, "notify-2026-10-29.log"), "too old\n");
        File.WriteAllText(Path.Combine(logs, "other-2026-11-06.log"), "not ours\n");
        File.WriteAllText(Path.Combine(logs, "notes.txt"), "not a log\n");
        var reports = Path.Combine(paths.DataDirectory, "reports");

        var zip = LogBundle.Create(logs, reports, "tm-publisher", "Program: TM Publisher\nUser: someone@example.com\n", Now, home: home);

        Path.GetFileName(zip).ShouldBe("tm-publisher-logs-20261106-1430.zip");
        using var archive = ZipFile.OpenRead(zip);
        archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal)
            .ShouldBe(["logs/app-2026-11-06.log", "logs/scheduler-2026-11-01.log", "system.txt"]);
        var all = string.Join("\n", archive.Entries.Select(e =>
        {
            using var reader = new StreamReader(e.Open());
            return reader.ReadToEnd();
        }));
        all.ShouldNotContain("someone@example.com");
        all.ShouldNotContain(home);
        all.ShouldNotContain("abcdefghijklmnop");
        all.ShouldContain("opened ~" + sep + "Videos");
    }

    [Fact]
    public void OnlyTheNewestFiveZipsStay()
    {
        using var paths = new TempPaths();
        var logs = Directory.CreateDirectory(paths.LogDirectory).FullName;
        var reports = Directory.CreateDirectory(Path.Combine(paths.DataDirectory, "reports")).FullName;
        for (var day = 1; day <= 6; day++)
        {
            File.WriteAllText(Path.Combine(reports, $"tm-publisher-logs-202611{day:00}-0900.zip"), "old");
        }

        File.WriteAllText(Path.Combine(reports, "keep-me.zip"), "not a report");

        LogBundle.Create(logs, reports, "tm-publisher", "x", Now);

        Directory.GetFiles(reports, "tm-publisher-logs-*.zip").Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(
        [
            "tm-publisher-logs-20261103-0900.zip", "tm-publisher-logs-20261104-0900.zip", "tm-publisher-logs-20261105-0900.zip",
            "tm-publisher-logs-20261106-0900.zip", "tm-publisher-logs-20261106-1430.zip",
        ]);
        File.Exists(Path.Combine(reports, "keep-me.zip")).ShouldBeTrue();
    }

    [Fact]
    public void TheIssueAddressEscapesValuesAndStaysShort()
    {
        var address = IssueLink.For("TM-AutoClip/tm-publisher", "bug_report.yml",
        [
            new("version", "0.1.4+abc1234"),
            new("os", "Microsoft Windows 10.0 & more"),
            new("architecture", new string('a', 5000)),
        ]).AbsoluteUri;

        address.ShouldStartWith("https://github.com/TM-AutoClip/tm-publisher/issues/new?template=bug_report.yml&version=0.1.4%2Babc1234&os=Microsoft%20Windows%2010.0%20%26%20more");
        address.ShouldContain("&architecture=" + new string('a', IssueLink.MaxValue));
        address.ShouldNotContain(new string('a', IssueLink.MaxValue + 1));
        address.Length.ShouldBeLessThan(IssueLink.MaxLength);
    }

    [Fact]
    public void SystemTxtHasTheProgramAndNoUserFolder()
    {
        var text = SystemReport.Text("TM Publisher", [new("Interface language", "tr"), new("Folder", LogScrubber.Home)]);
        text.ShouldContain("Program: TM Publisher");
        text.ShouldContain("Version: " + LogHeader.Version);
        text.ShouldContain("Architecture: " + LogHeader.Architecture);
        text.ShouldContain("Interface language: tr");
        text.ShouldNotContain(LogScrubber.Home);
    }

    [Fact]
    public void OldVersionLogFilesNeverGoIntoTheZip()
    {
        using var paths = new TempPaths();
        var logs = Directory.CreateDirectory(paths.LogDirectory).FullName;
        File.WriteAllText(Path.Combine(logs, "tmpublisher-20261106.log"), "Post 3 blocked before sending: X · My Account Name: too long\n");
        File.WriteAllText(Path.Combine(logs, "tmpublisher-scheduler-20261105.log"), "old\n");
        File.WriteAllText(Path.Combine(logs, "app-2026-11-06.log"), "new\n");

        LogBundle.LogFiles(logs, DateOnly.FromDateTime(Now.DateTime)).Select(Path.GetFileName).ShouldBe(["app-2026-11-06.log"]);
        var zip = LogBundle.Create(logs, Path.Combine(paths.DataDirectory, "reports"), "tm-publisher", "x", Now);
        using var archive = ZipFile.OpenRead(zip);
        archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ShouldBe(["logs/app-2026-11-06.log", "system.txt"]);
    }

    [Fact]
    public void ZipsOfTheSameMinuteArePrunedInTheOrderTheyWereMade()
    {
        using var paths = new TempPaths();
        var logs = Directory.CreateDirectory(paths.LogDirectory).FullName;
        var reports = Directory.CreateDirectory(Path.Combine(paths.DataDirectory, "reports")).FullName;
        File.WriteAllText(Path.Combine(reports, "tm-publisher-logs-20261106-1430.zip"), "old");
        for (var i = 2; i <= 10; i++)
        {
            File.WriteAllText(Path.Combine(reports, $"tm-publisher-logs-20261106-1430-{i}.zip"), "old");
        }

        var zip = LogBundle.Create(logs, reports, "tm-publisher", "x", Now);

        Path.GetFileName(zip).ShouldBe("tm-publisher-logs-20261106-1430-11.zip");
        Directory.GetFiles(reports).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(
        [
            "tm-publisher-logs-20261106-1430-10.zip", "tm-publisher-logs-20261106-1430-11.zip", "tm-publisher-logs-20261106-1430-7.zip",
            "tm-publisher-logs-20261106-1430-8.zip", "tm-publisher-logs-20261106-1430-9.zip",
        ]);
    }

    [Fact]
    public void AFailedZipLeavesNothingToAttach()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // The failure is made with a file opened without sharing, which Windows enforces for every reader.
        }

        using var paths = new TempPaths();
        var logs = Directory.CreateDirectory(paths.LogDirectory).FullName;
        var reports = Path.Combine(paths.DataDirectory, "reports");
        var held = Path.Combine(logs, "app-2026-11-06.log");
        File.WriteAllText(held, "line\n");

        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Should.Throw<IOException>(() => LogBundle.Create(logs, reports, "tm-publisher", "x", Now));
        }

        Directory.GetFiles(reports).ShouldBeEmpty();
    }
}
