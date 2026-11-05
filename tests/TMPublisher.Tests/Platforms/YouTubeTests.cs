using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.YouTube;

namespace TMPublisher.Tests.Platforms;

public sealed class YouTubeTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task ConnectUsesLoopbackPkceAndReadsTheChannel()
    {
        var prompt = new FakePrompt();
        var loopback = new FakeLoopback(prompt);
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "oauth2.googleapis.com/token", "{\"access_token\":\"a1\",\"refresh_token\":\"r1\",\"expires_in\":3599}")
            .Json(HttpMethod.Get, "channels?part=snippet&mine=true", "{\"items\":[{\"id\":\"UC1\",\"snippet\":{\"title\":\"Example channel\",\"customUrl\":\"@example\"}}]}");
        var platform = new YouTubePlatform(http.Transport, TimeProvider.System, loopback, NullLoggerFactory.Instance);
        var input = new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.ClientId] = "cid.apps.googleusercontent.com", [CredentialKeys.ClientSecret] = "sec" });
        var result = await platform.Connection.ConnectAsync(input, prompt, None);

        loopback.Port.ShouldBe(YouTubePlatform.LoopbackPort);
        var opened = prompt.Opened.Single().AbsoluteUri;
        opened.ShouldContain("code_challenge_method=S256");
        opened.ShouldContain("redirect_uri=http%3A%2F%2F127.0.0.1%3A47811%2Fcallback");
        opened.ShouldContain("access_type=offline");
        var exchange = http.To("oauth2.googleapis.com/token").Single().Body;
        exchange.ShouldContain("code=the-code");
        exchange.ShouldContain("code_verifier=");
        result.Identity.ExternalId.ShouldBe("UC1");
        result.Credentials[CredentialKeys.RefreshToken].ShouldBe("r1");
        result.Credentials.AccessExpiresAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task RevokedRefreshTokenIsAnAuthError()
    {
        var http = new FakeHttp().Json(HttpMethod.Post, "oauth2.googleapis.com/token", "{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest);
        var refresher = new GoogleTokenRefresher(http.Transport, TimeProvider.System);
        var creds = Credentials.Empty.With((CredentialKeys.RefreshToken, "r"), (CredentialKeys.ClientId, "c"));
        await Should.ThrowAsync<PlatformAuthException>(() => refresher.RefreshAsync(creds, None));
    }

    [Fact]
    public async Task ResumableUploadSendsChunksResumesAfterAnErrorAndReportsProcessing()
    {
        using var media = new TestMedia();
        var chunkCalls = 0;
        var http = new FakeHttp()
            .On(HttpMethod.Post, "upload/youtube/v3/videos?uploadType=resumable", (_, body) =>
            {
                body.ShouldContain("\"privacyStatus\":\"unlisted\"");
                body.ShouldContain("\"tags\":[\"one\"]");
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Location = new Uri("https://upload.example/session/1");
                return response;
            })
            .On(HttpMethod.Put, "upload.example/session/1", (request, _) =>
            {
                var range = request.Content!.Headers.ContentRange!;
                if (!range.HasRange)
                {
                    // Resume point question: the first chunk was received.
                    var ask = new HttpResponseMessage((HttpStatusCode)308);
                    ask.Headers.TryAddWithoutValidation("Range", "bytes=0-2047");
                    return ask;
                }

                chunkCalls++;
                if (chunkCalls == 2)
                {
                    return FakeHttp.Response(HttpStatusCode.ServiceUnavailable, "{}");
                }

                if (range.To + 1 < range.Length)
                {
                    var partial = new HttpResponseMessage((HttpStatusCode)308);
                    partial.Headers.TryAddWithoutValidation("Range", $"bytes=0-{range.To}");
                    return partial;
                }

                return FakeHttp.Response(HttpStatusCode.OK, "{\"id\":\"vid1\"}");
            });
        var publisher = new YouTubePublisher(http.Transport, NullLogger<YouTubePublisher>.Instance) { ChunkBytes = 2048, Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video(bytes: 5000)] };
        var request = TestMedia.Request(post, "Description", Credentials.Empty.With((CredentialKeys.AccessToken, "t")), title: "Title",
            options: new() { [TargetOptionKeys.Privacy] = "unlisted" });
        request.Target.Keywords = ["one"];
        var result = await publisher.PublishAsync(request, null, None);

        result.ExternalId.ShouldBe("vid1");
        result.State.ShouldBe(TargetStatus.Processing);
        result.Url.ShouldBe("https://youtu.be/vid1");
    }

    [Fact]
    public async Task QuotaErrorIsAQuotaException()
    {
        using var media = new TestMedia();
        var http = new FakeHttp().Json(HttpMethod.Post, "uploadType=resumable",
            "{\"error\":{\"errors\":[{\"reason\":\"quotaExceeded\"}],\"message\":\"quota\"}}", HttpStatusCode.Forbidden);
        var publisher = new YouTubePublisher(http.Transport, NullLogger<YouTubePublisher>.Instance);
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        await Should.ThrowAsync<PlatformQuotaException>(() =>
            publisher.PublishAsync(TestMedia.Request(post, "d", Credentials.Empty.With((CredentialKeys.AccessToken, "t")), title: "T"), null, None));
    }

    [Theory]
    [InlineData("{\"items\":[{\"status\":{\"uploadStatus\":\"processed\"}}]}", TargetStatus.Published)]
    [InlineData("{\"items\":[{\"status\":{\"uploadStatus\":\"uploaded\"},\"processingDetails\":{\"processingStatus\":\"processing\"}}]}", TargetStatus.Processing)]
    [InlineData("{\"items\":[{\"status\":{\"uploadStatus\":\"rejected\",\"rejectionReason\":\"duplicate\"}}]}", TargetStatus.Failed)]
    public async Task ProcessingCheckReadsTheVideoState(string answer, TargetStatus expected)
    {
        var http = new FakeHttp().Json(HttpMethod.Get, "videos?part=status", answer);
        var publisher = new YouTubePublisher(http.Transport, NullLogger<YouTubePublisher>.Instance);
        var request = TestMedia.Request(new Post(), string.Empty, Credentials.Empty.With((CredentialKeys.AccessToken, "t")));
        (await publisher.CheckProcessingAsync(request, "vid1", None)).State.ShouldBe(expected);
    }
}
