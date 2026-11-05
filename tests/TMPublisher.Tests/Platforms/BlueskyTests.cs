using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Platforms.Bluesky;

namespace TMPublisher.Tests.Platforms;

public sealed class BlueskyTests
{
    private static string Jwt(long exp) =>
        "eyJhbGciOiJub25lIn0." + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":" + exp + "}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".sig";

    [Fact]
    public async Task ConnectCreatesASessionAndReadsTheExpiryFromTheJwt()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "createSession", "{\"accessJwt\":\"" + Jwt(2000000000) + "\",\"refreshJwt\":\"r1\",\"did\":\"did:plc:example\"}")
            .Json(HttpMethod.Get, "getSession", "{\"did\":\"did:plc:example\",\"handle\":\"example.bsky.social\"}");
        var platform = new BlueskyPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var input = new ConnectionInput(new Dictionary<string, string>
        {
            [CredentialKeys.Handle] = "example.bsky.social",
            [CredentialKeys.AppPassword] = "aaaa-bbbb-cccc-dddd",
            [CredentialKeys.Server] = "https://bsky.social",
        });
        var result = await platform.Connection.ConnectAsync(input, new FakePrompt(), CancellationToken.None);
        result.Identity.Handle.ShouldBe("@example.bsky.social");
        result.Credentials.AccessExpiresAtUtc.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(2000000000));
    }

    [Fact]
    public async Task PhotoPostUploadsBlobsAddsFacetsAndRenewsAnExpiredToken()
    {
        using var media = new TestMedia();
        var calls = 0;
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "uploadBlob", "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafy\"},\"mimeType\":\"image/jpeg\",\"size\":1000}}")
            .On(HttpMethod.Post, "createRecord", (request, _) => ++calls == 1
                ? FakeHttp.Response(HttpStatusCode.BadRequest, "{\"error\":\"ExpiredToken\",\"message\":\"Token has expired\"}")
                : FakeHttp.Response(HttpStatusCode.OK, "{\"uri\":\"at://did:plc:example/app.bsky.feed.post/3kabc\",\"cid\":\"x\"}"));
        var publisher = new BlueskyPublisher(http.Transport, NullLogger<BlueskyPublisher>.Instance);
        var creds = Credentials.Empty.With((CredentialKeys.AccessToken, "old"), (CredentialKeys.UserId, "did:plc:example"));
        var renewed = 0;
        var post = new Post { Kind = PostKind.Photo, Media = [media.Photo()] };
        var request = TestMedia.Request(post, "Çok güzel #haber https://example.com", creds,
            _ => { renewed++; return Task.FromResult(creds.With((CredentialKeys.AccessToken, "new"))); }, "example.bsky.social");
        var result = await publisher.PublishAsync(request, null, CancellationToken.None);

        renewed.ShouldBe(1);
        result.Url.ShouldBe("https://bsky.app/profile/example.bsky.social/post/3kabc");
        var record = http.To("createRecord").Last();
        record.Request.Headers.Authorization!.Parameter.ShouldBe("new");
        record.Body.ShouldContain("app.bsky.embed.images");
        record.Body.ShouldContain("app.bsky.richtext.facet#tag");
        record.Body.ShouldContain("app.bsky.richtext.facet#link");
    }

    [Fact]
    public void FacetOffsetsAreUtf8Bytes()
    {
        var facets = BlueskyFacets.For("ğ #a");
        var index = facets.Single()!["index"]!;
        index["byteStart"]!.GetValue<int>().ShouldBe(3);
        index["byteEnd"]!.GetValue<int>().ShouldBe(5);
    }
}
