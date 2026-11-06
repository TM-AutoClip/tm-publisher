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
}
