using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Platforms.Mastodon;

namespace TMPublisher.Tests.Platforms;

public sealed class MastodonTests
{
    [Fact]
    public async Task ConnectRegistersTheAppOpensSignInAndExchangesThePastedCode()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "/api/v1/apps", "{\"client_id\":\"cid\",\"client_secret\":\"cs\"}")
            .Json(HttpMethod.Post, "/oauth/token", "{\"access_token\":\"tok\",\"token_type\":\"Bearer\"}")
            .Json(HttpMethod.Get, "verify_credentials", "{\"id\":\"1\",\"username\":\"example\",\"acct\":\"example\",\"display_name\":\"Example\"}");
        var platform = new MastodonPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var prompt = new FakePrompt(_ => "the-code");
        var result = await platform.Connection.ConnectAsync(new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.Server] = "mastodon.example" }), prompt, CancellationToken.None);

        prompt.Opened.Single().AbsoluteUri.ShouldStartWith("https://mastodon.example/oauth/authorize?response_type=code&client_id=cid");
        http.To("/oauth/token").Single().Body.ShouldContain("code=the-code");
        result.Identity.Handle.ShouldBe("@example@mastodon.example");
        result.Credentials[CredentialKeys.AccessToken].ShouldBe("tok");
    }

    [Fact]
    public async Task MediaStillProcessingIsAwaitedThenTheStatusUsesTheVisibility()
    {
        using var media = new TestMedia();
        var polls = 0;
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "/api/v2/media", "{\"id\":\"m1\",\"url\":null}", HttpStatusCode.Accepted)
            .On(HttpMethod.Get, "/api/v1/media/m1", (_, _) => FakeHttp.Response(polls++ == 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK,
                polls == 1 ? "{\"id\":\"m1\",\"url\":null}" : "{\"id\":\"m1\",\"url\":\"https://files.example/m1.mp4\"}"))
            .Json(HttpMethod.Post, "/api/v1/statuses", "{\"id\":\"s1\",\"url\":\"https://mastodon.example/@example/s1\"}");
        var publisher = new MastodonPublisher(http.Transport, NullLogger<MastodonPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var creds = Credentials.Empty.With((CredentialKeys.Server, "https://mastodon.example"), (CredentialKeys.AccessToken, "tok"));
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Hello", creds, options: new() { [TargetOptionKeys.Privacy] = "unlisted" }), null, CancellationToken.None);

        result.Url.ShouldBe("https://mastodon.example/@example/s1");
        var status = http.To("/api/v1/statuses").Single();
        status.Body.ShouldContain("\"visibility\":\"unlisted\"");
        status.Body.ShouldContain("\"media_ids\":[\"m1\"]");
    }
}
