using System.Net;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.Updates;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Tests.Platforms;

namespace TMPublisher.Tests.Desktop;

/// <summary>
/// The look for a new version: GitHub's newest release compared with this version, the ETag sent again (304 = unchanged), GitHub's rate limit
/// respected without a request, offline keeps the last answer, the start never waits for it, and the switch in Settings. No real network.
/// </summary>
public sealed class UpdateCheckTests
{
    private const string Newer = """{"tag_name":"v99.0.0","html_url":"https://github.com/TM-AutoClip/tm-publisher/releases/tag/v99.0.0","draft":false,"prerelease":false}""";

    private static HttpResponseMessage Release(string json, string etag)
    {
        var response = FakeHttp.Response(HttpStatusCode.OK, json);
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        return response;
    }

    [Theory]
    [InlineData("v0.1.7", "0.1.7")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("v1.2", "1.2.0")]
    [InlineData("v0.2.0-beta", null)]
    [InlineData("v1", null)]
    [InlineData("latest", null)]
    [InlineData("", null)]
    public void TagsAreReadAsVersions(string tag, string? expected) =>
        ReleaseChecker.ParseTag(tag)?.ToString(3).ShouldBe(expected);

    [Fact]
    public void OnlyANewerVersionIsOfferedAndOnlyThisRepositorysPageIsOpened()
    {
        var current = new Version(0, 1, 7);
        ReleaseChecker.Compare("v0.1.8", "https://github.com/TM-AutoClip/tm-publisher/releases/tag/v0.1.8", current).Outcome.ShouldBe(UpdateOutcome.NewVersion);
        ReleaseChecker.Compare("v0.1.7", null, current).Outcome.ShouldBe(UpdateOutcome.UpToDate);
        ReleaseChecker.Compare("v0.1.6", null, current).Outcome.ShouldBe(UpdateOutcome.UpToDate);
        ReleaseChecker.Compare("v0.1.8", "https://example.com/evil", current).Page.ShouldBe(UpdateRules.DownloadPage);
        ReleaseChecker.Compare("v0.1.8", "https://github.com/TM-AutoClip/tm-publisher/releases/tag/v0.1.8", current).Page.AbsoluteUri
            .ShouldBe("https://github.com/TM-AutoClip/tm-publisher/releases/tag/v0.1.8");
    }

    [Fact]
    public async Task ANewReleaseIsFoundAndTheETagIsSentAgain()
    {
        var http = new FakeHttp();
        var calls = 0;
        http.On(HttpMethod.Get, "/releases/latest", (request, _) =>
        {
            calls++;
            return request.Headers.IfNoneMatch.Count > 0
                ? new HttpResponseMessage(HttpStatusCode.NotModified)
                : Release(Newer, "\"abc\"");
        });
        using var h = new DesktopHarness(s => s.AddSingleton(http.Transport));
        var checker = h.Services.GetRequiredService<ReleaseChecker>();

        var first = await checker.CheckAsync(TestContext.Current.CancellationToken);
        first.Outcome.ShouldBe(UpdateOutcome.NewVersion);
        first.Latest.ShouldBe(new Version(99, 0, 0));
        first.Page.AbsoluteUri.ShouldEndWith("/releases/tag/v99.0.0");
        http.Requests[0].Request.Headers.UserAgent.ToString().ShouldStartWith("TMPublisher/");
        checker.ReadState().ETag.ShouldBe("\"abc\"");

        var second = await checker.CheckAsync(TestContext.Current.CancellationToken);
        calls.ShouldBe(2);
        http.Requests[1].Request.Headers.IfNoneMatch.ToString().ShouldBe("\"abc\"");
        second.Outcome.ShouldBe(UpdateOutcome.NewVersion);
        second.Latest.ShouldBe(new Version(99, 0, 0));
    }

    [Fact]
    public async Task TheRateLimitIsRespectedWithoutARequestAndTheKnownReleaseStays()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-25T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var reset = clock.GetUtcNow().AddMinutes(40);
        var http = new FakeHttp();
        var limited = false;
        http.On(HttpMethod.Get, "/releases/latest", (_, _) =>
        {
            if (!limited)
            {
                return Release(Newer, "\"abc\"");
            }

            var response = FakeHttp.Response(HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""");
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return response;
        });
        using var h = new DesktopHarness(s =>
        {
            s.AddSingleton(http.Transport);
            s.AddSingleton<TimeProvider>(clock);
        });
        var checker = h.Services.GetRequiredService<ReleaseChecker>();
        (await checker.CheckAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateOutcome.NewVersion);

        limited = true;
        var refused = await checker.CheckAsync(TestContext.Current.CancellationToken);
        refused.Outcome.ShouldBe(UpdateOutcome.NewVersion);
        refused.NotBeforeUtc.ShouldBe(reset);
        http.Requests.Count.ShouldBe(2);

        clock.Advance(TimeSpan.FromMinutes(10));
        var waiting = await checker.CheckAsync(TestContext.Current.CancellationToken);
        http.Requests.Count.ShouldBe(2);
        waiting.Latest.ShouldBe(new Version(99, 0, 0));

        limited = false;
        clock.Advance(TimeSpan.FromMinutes(31));
        await checker.CheckAsync(TestContext.Current.CancellationToken);
        http.Requests.Count.ShouldBe(3);
        checker.ReadState().NotBeforeUtc.ShouldBeNull();
    }

    [Fact]
    public async Task OfflineAndNoReleaseAreToldWithoutAnError()
    {
        var http = new FakeHttp();
        http.On(HttpMethod.Get, "/releases/latest", (_, _) => throw new HttpRequestException("no route"));
        using (var h = new DesktopHarness(s => s.AddSingleton(http.Transport)))
        {
            (await h.Services.GetRequiredService<ReleaseChecker>().CheckAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateOutcome.Unreachable);
        }

        // The harness's fake answers 404 for every address: no published release.
        using var empty = new DesktopHarness();
        (await empty.Services.GetRequiredService<ReleaseChecker>().CheckAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateOutcome.NoRelease);
    }

    [Fact]
    public async Task TheStartDoesNotWaitForGitHub()
    {
        using var answer = new SemaphoreSlim(0);
        var http = new FakeHttp();
        http.On(HttpMethod.Get, "/releases/latest", (_, _) =>
        {
            answer.Wait(TimeSpan.FromSeconds(10));
            return Release(Newer, "\"abc\"");
        });
        using var h = new DesktopHarness(s => s.AddSingleton(http.Transport));
        var watcher = h.Services.GetRequiredService<UpdateWatcher>();
        h.Services.GetServices<IStartupTask>().ShouldContain(watcher);

        var start = watcher.StartAsync(TestContext.Current.CancellationToken);
        start.IsCompleted.ShouldBeTrue();
        watcher.Last.Outcome.ShouldBe(UpdateOutcome.NotChecked);

        answer.Release();
        await watcher.Running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        watcher.Last.Outcome.ShouldBe(UpdateOutcome.NewVersion);
    }

    [Fact]
    public async Task TurnedOffTheStartSendsNothing()
    {
        var http = new FakeHttp();
        using var h = new DesktopHarness(s => s.AddSingleton(http.Transport));
        var preferences = h.Services.GetRequiredService<UiPreferencesFile>();
        preferences.Write(new UiPreferences { Language = "tr", CheckForUpdates = false });

        var watcher = h.Services.GetRequiredService<UpdateWatcher>();
        await watcher.StartAsync(TestContext.Current.CancellationToken);
        await watcher.Running;
        watcher.Last.Outcome.ShouldBe(UpdateOutcome.Off);
        http.Requests.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheWindowShowsTheNewVersionAndOpensItsPage()
    {
        var http = new FakeHttp().On(HttpMethod.Get, "/releases/latest", (_, _) => Release(Newer, "\"abc\""));
        var os = new FakeReportOs();
        using var h = new DesktopHarness(s =>
        {
            s.AddSingleton(http.Transport);
            s.AddSingleton<IUrlOpener>(os);
        });
        var shell = h.Services.GetRequiredService<ShellViewModel>();
        shell.UpdateAvailable.ShouldBeFalse();

        await h.Services.GetRequiredService<UpdateWatcher>().CheckAsync(CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        shell.UpdateAvailable.ShouldBeTrue();
        shell.UpdateText.ShouldContain("99.0.0");
        shell.OpenUpdatePageCommand.Execute(null);
        os.Opened.ShouldHaveSingleItem().AbsoluteUri.ShouldEndWith("/releases/tag/v99.0.0");
    }

    [AvaloniaFact]
    public async Task SettingsKeepsTheSwitchAndChecksNow()
    {
        var http = new FakeHttp().On(HttpMethod.Get, "/releases/latest", (_, _) => Release(Newer, "\"abc\""));
        using var h = new DesktopHarness(s => s.AddSingleton(http.Transport));
        var preferences = h.Services.GetRequiredService<UiPreferencesFile>();
        preferences.Write(new UiPreferences { Language = "en" });
        var settings = h.Services.GetRequiredService<SettingsViewModel>();
        await settings.ActivateAsync();
        settings.CheckForUpdates.ShouldBeTrue();

        settings.CheckForUpdates = false;
        preferences.Read().CheckForUpdates.ShouldBeFalse();
        preferences.Read().Language.ShouldBe("en");

        await settings.CheckUpdatesCommand.ExecuteAsync(null);
        settings.UpdateAvailable.ShouldBeTrue();
        settings.UpdateStatus.ShouldContain("99.0.0");
        http.Requests.Count.ShouldBe(1);
    }
}
