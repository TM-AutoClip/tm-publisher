using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.X;

namespace TMPublisher.Tests.Platforms;

public sealed class XTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task ConnectIsAPublicClientWithoutSecret()
    {
        var prompt = new FakePrompt();
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "oauth2/token", "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":7200}")
            .Json(HttpMethod.Get, "users/me", "{\"data\":{\"id\":\"9\",\"name\":\"Example\",\"username\":\"example\"}}");
        var platform = new XPlatform(http.Transport, TimeProvider.System, new FakeLoopback(prompt), NullLoggerFactory.Instance);
        platform.Connection.Fields.ShouldNotContain(f => f.Key == CredentialKeys.ClientSecret);
        var result = await platform.Connection.ConnectAsync(new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.ClientId] = "cid" }), prompt, None);
        result.Identity.Handle.ShouldBe("@example");
        http.To("oauth2/token").Single().Body.ShouldNotContain("client_secret");
        prompt.Opened.Single().AbsoluteUri.ShouldContain("offline.access");
    }

    [Fact]
    public async Task PhotoPostUploadsMediaThenPostsWithTheRenewedToken()
    {
        using var media = new TestMedia();
        var posts = 0;
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "media/upload/initialize", "{\"data\":{\"id\":\"m1\"}}")
            .Json(HttpMethod.Post, "/append", "{}")
            .Json(HttpMethod.Post, "/finalize", "{\"data\":{\"id\":\"m1\"}}")
            .On(HttpMethod.Post, "/2/tweets", (_, _) => posts++ == 0
                ? FakeHttp.Response(HttpStatusCode.Unauthorized, "{\"title\":\"Unauthorized\"}")
                : FakeHttp.Response(HttpStatusCode.Created, "{\"data\":{\"id\":\"t1\"}}"));
        var publisher = new XPublisher(http.Transport, NullLogger<XPublisher>.Instance) { ChunkBytes = 400 };
        var creds = Credentials.Empty.With((CredentialKeys.AccessToken, "old"));
        var post = new Post { Kind = PostKind.Photo, Media = [media.Photo(bytes: 1000)] };
        var request = TestMedia.Request(post, "Hello", creds, _ => Task.FromResult(creds.With((CredentialKeys.AccessToken, "new"))));
        var result = await publisher.PublishAsync(request, null, None);

        result.Url.ShouldBe("https://x.com/i/status/t1");
        http.To("/append").Count().ShouldBe(3);
        http.To("media/upload/initialize").Single().Body.ShouldContain("tweet_image");
        var tweet = http.To("/2/tweets").Last();
        tweet.Request.Headers.Authorization!.Parameter.ShouldBe("new");
        tweet.Body.ShouldContain("\"media_ids\":[\"m1\"]");
    }

    [Fact]
    public async Task TooLongTextIsRejectedBeforeAnyRequest()
    {
        var http = new FakeHttp();
        var publisher = new XPublisher(http.Transport, NullLogger<XPublisher>.Instance);
        await Should.ThrowAsync<PlatformRejectedException>(() =>
            publisher.PublishAsync(TestMedia.Request(new Post { Kind = PostKind.Text }, new string('a', 281), Credentials.Empty.With((CredentialKeys.AccessToken, "t"))), null, None));
        http.Requests.ShouldBeEmpty();
    }
}
