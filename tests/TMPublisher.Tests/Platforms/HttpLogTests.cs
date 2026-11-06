using System.Net;
using Microsoft.Extensions.Logging;
using Shouldly;
using TMPublisher.Platforms.Common;
using TMPublisher.Tests.Runtime;

namespace TMPublisher.Tests.Platforms;

/// <summary>One log line per platform request, without ids, tokens, queries or the user's own server name.</summary>
public sealed class HttpLogTests
{
    [Theory]
    [InlineData("https://api.telegram.org/bot123456:ABC-def_ghi/sendPhoto?chat_id=5", "api.telegram.org", "/*/sendPhoto")]
    [InlineData("https://discord.com/api/webhooks/123456789/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789abcdefghijklmnopqrstuvwxyzABCD", "discord.com", "/api/webhooks/*/*")]
    [InlineData("https://graph.facebook.com/v21.0/17841400000000000/media?access_token=x", "graph.facebook.com", "/v21.0/*/media")]
    [InlineData("https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable", "www.googleapis.com", "/upload/youtube/v3/videos")]
    [InlineData("https://my-own-name.example/api/v1/statuses", PlatformHosts.Other, "/api/v1/statuses")]
    public void AddressesAreLoggedWithoutIdsTokensOrOwnServers(string address, string host, string path)
    {
        var uri = new Uri(address);
        HttpLogHandler.Host(uri).ShouldBe(host);
        HttpLogHandler.Path(uri).ShouldBe(path);
    }

    [Fact]
    public async Task EachRequestLeavesOneLineWithStatusAndDuration()
    {
        using var recorder = new RecordingLoggerProvider();
        var fake = new FakeHttp().Json(HttpMethod.Get, "/2/users/me", "{}").Json(HttpMethod.Post, "/2/tweets", "{\"error\":1}", HttpStatusCode.TooManyRequests);
        using var client = fake.Transport.WithLog(recorder.CreateLogger("TMPublisher.Http")).CreateClient(TimeSpan.FromSeconds(5));

        using var ok = await client.GetAsync(new Uri("https://api.x.com/2/users/me?secret=1"), TestContext.Current.CancellationToken);
        using var limited = await client.PostAsync(new Uri("https://api.x.com/2/tweets"), new StringContent("my post text"), TestContext.Current.CancellationToken);

        var lines = recorder.Entries.ToList();
        lines.Count.ShouldBe(2);
        lines[0].Level.ShouldBe(LogLevel.Information);
        lines[0].Text.ShouldStartWith("HTTP GET api.x.com/2/users/me: 200 in ");
        lines[1].Level.ShouldBe(LogLevel.Warning);
        lines[1].Text.ShouldStartWith("HTTP POST api.x.com/2/tweets: 429 in ");
        lines.ShouldAllBe(l => !l.Text.Contains("secret") && !l.Text.Contains("my post text"));
    }

    [Fact]
    public async Task AClientTimeOutIsAWarningWithoutAnswerNotACancel()
    {
        using var recorder = new RecordingLoggerProvider();
        var transport = new HttpTransport(() => new SilentHttp()).WithLog(recorder.CreateLogger("TMPublisher.Http"));
        using var client = transport.CreateClient(TimeSpan.FromMilliseconds(400));

        // The exception callers retry on stays HttpClient's own (TaskCanceledException); only the log line changes.
        await Should.ThrowAsync<TaskCanceledException>(() => client.GetAsync(new Uri("https://api.x.com/2/users/me"), TestContext.Current.CancellationToken));

        var line = recorder.Entries.Single();
        line.Level.ShouldBe(LogLevel.Warning);
        line.Text.ShouldStartWith("HTTP GET api.x.com/2/users/me: no answer within the time-out of 400 ms");
    }

    [Fact]
    public async Task TheCallersOwnCancelStaysInformation()
    {
        using var recorder = new RecordingLoggerProvider();
        var transport = new HttpTransport(() => new SilentHttp()).WithLog(recorder.CreateLogger("TMPublisher.Http"));
        using var client = transport.CreateClient(TimeSpan.FromSeconds(30));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(() => client.GetAsync(new Uri("https://api.x.com/2/users/me"), stop.Token));

        var line = recorder.Entries.Single();
        line.Level.ShouldBe(LogLevel.Information);
        line.Text.ShouldStartWith("HTTP GET api.x.com/2/users/me: canceled after ");
    }

    [Fact]
    public async Task TheUsersOwnServerNameDoesNotReachTheLogThroughAConnectionError()
    {
        using var paths = new TMPublisher.Tests.Storage.TempPaths();
        Directory.CreateDirectory(paths.LogDirectory);
        var fake = new FakeHttp().On(HttpMethod.Post, "/xrpc/", (_, _) =>
            throw new HttpRequestException("No such host is known. (pds.own-domain-4711.example:443)", new IOException("No such host is known.")));
        using (var file = new TMPublisher.Runtime.Logging.FileLoggerProvider(paths.LogDirectory, TMPublisher.Runtime.Logging.LogKinds.Scheduler))
        {
            using var client = fake.Transport.WithLog(file.CreateLogger("TMPublisher.Http")).CreateClient(TimeSpan.FromSeconds(5));
            var ex = await Should.ThrowAsync<HttpRequestException>(() =>
                client.PostAsync(new Uri("https://pds.own-domain-4711.example/xrpc/com.atproto.repo.createRecord"), new StringContent("{}"),
                    TestContext.Current.CancellationToken));

            // What the post sender writes for the failed post: the whole exception.
            file.CreateLogger("TMPublisher.Scheduling").LogError(ex, "Post {PostId} failed: {Reason}", 7, ex.Message);
        }

        var text = File.ReadAllText(Directory.GetFiles(paths.LogDirectory, "scheduler-*.log").Single());
        text.ShouldContain("HTTP POST (account server)/xrpc/com.atproto.repo.createRecord: no answer after ");
        text.ShouldContain("Post 7 failed: No such host is known. ((account server):443)");
        text.ShouldNotContain("own-domain-4711", Case.Insensitive);
    }
}
