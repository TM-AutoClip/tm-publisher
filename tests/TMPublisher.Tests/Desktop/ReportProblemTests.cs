using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Tests.Runtime;

namespace TMPublisher.Tests.Desktop;

/// <summary>"Report a problem" in the window, and the window's unhandled exceptions.</summary>
public sealed class ReportProblemTests
{
    private static (DesktopHarness Harness, FakeReportOs Os, RecordingLoggerProvider Log) Make()
    {
        var os = new FakeReportOs();
        var log = new RecordingLoggerProvider();
        var harness = new DesktopHarness(s =>
        {
            s.AddSingleton<IUrlOpener>(os);
            s.AddSingleton<IFileReveal>(os);
            s.AddSingleton<ILoggerProvider>(log);
        });
        return (harness, os, log);
    }

    [Fact]
    public async Task PrepareLogsMakesTheZipInReportsAndShowsIt()
    {
        var (h, os, _) = Make();
        using var harness = h;
        h.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Test").LogInformation("a line for the zip");
        var model = h.Services.GetRequiredService<ReportProblemViewModel>();

        await model.PrepareLogsCommand.ExecuteAsync(null);

        model.ZipPath.ShouldNotBeNull();
        Path.GetDirectoryName(model.ZipPath).ShouldBe(h.Paths.ReportDirectory);
        os.Revealed.ShouldBe([model.ZipPath]);
        using var archive = ZipFile.OpenRead(model.ZipPath);
        archive.Entries.Select(e => e.FullName).ShouldContain("system.txt");
        archive.Entries.ShouldContain(e => e.FullName.StartsWith("logs/app-", StringComparison.Ordinal));
        os.Opened.ShouldBeEmpty();
    }

    [Fact]
    public void ReportOnGitHubOpensTheFormThroughTheOpenerOnly()
    {
        var (h, os, _) = Make();
        using var harness = h;
        var model = h.Services.GetRequiredService<ReportProblemViewModel>();

        model.ReportOnGitHubCommand.Execute(null);

        os.Opened.Count.ShouldBe(1);
        os.Opened[0].AbsoluteUri.ShouldStartWith("https://github.com/TM-AutoClip/tm-publisher/issues/new?template=bug_report.yml&version=");
        os.Opened[0].AbsoluteUri.ShouldContain("&os=");
        os.Opened[0].AbsoluteUri.ShouldContain("&architecture=");
        model.Details.ShouldContain("Interface language: ");
        model.Details.ShouldContain("Drawing: processor");
    }

    [Fact]
    public void AnUnhandledWindowExceptionIsCritical()
    {
        var (h, _, log) = Make();
        using var harness = h;

        h.Services.GetRequiredService<AppStartup>().ReportUnhandled(new InvalidOperationException("from a button"));

        log.Entries.ShouldContain(e => e.Level == LogLevel.Critical && e.Exception is InvalidOperationException);
    }
}
