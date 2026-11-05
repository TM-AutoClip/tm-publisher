using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Platforms.LinkedIn;
using TMPublisher.Platforms.Pinterest;
using TMPublisher.Platforms.TikTok;

namespace TMPublisher.Tests.Platforms;

public sealed class TikTokLinkedInPinterestTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task TikTokUsesAHexChallengeAndTheClientKey()
    {
        var prompt = new FakePrompt();
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "oauth/token/", "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":86400,\"open_id\":\"o1\"}")
            .Json(HttpMethod.Get, "user/info/", "{\"data\":{\"user\":{\"open_id\":\"o1\",\"display_name\":\"Example\"}}}");
        var platform = new TikTokPlatform(http.Transport, TimeProvider.System, new FakeLoopback(prompt), NullLoggerFactory.Instance);
        var input = new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.ClientId] = "key", [CredentialKeys.ClientSecret] = "sec" });
        var result = await platform.Connection.ConnectAsync(input, prompt, None);

        var query = TMPublisher.Platforms.Connections.LoopbackListener.Parse(prompt.Opened.Single().Query);
        query["client_key"].ShouldBe("key");
        var verifier = TMPublisher.Platforms.Connections.LoopbackListener.Parse(http.To("oauth/token/").Single().Body)["code_verifier"];
        query["code_challenge"].ShouldBe(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).ToLowerInvariant());
        result.Identity.ExternalId.ShouldBe("o1");
    }

    [Fact]
    public async Task TikTokDraftGoesToTheInboxAndNeedsApproval()
    {
        using var media = new TestMedia();
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "inbox/video/init/", "{\"data\":{\"publish_id\":\"p1\",\"upload_url\":\"https://upload.example/u1\"},\"error\":{\"code\":\"ok\"}}")
            .Json(HttpMethod.Put, "upload.example/u1", "{}")
            .Json(HttpMethod.Post, "status/fetch/", "{\"data\":{\"status\":\"SEND_TO_USER_INBOX\"},\"error\":{\"code\":\"ok\"}}");
        var publisher = new TikTokPublisher(http.Transport, NullLogger<TikTokPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Caption", Credentials.Empty.With((CredentialKeys.AccessToken, "t"))), null, None);

        result.State.ShouldBe(TargetStatus.NeedsApproval);
        result.NoteKey.ShouldBe("note.tiktok_approve");
        http.To("inbox/video/init/").Single().Body.ShouldNotContain("post_info");
        http.To("upload.example").Single().Request.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task TikTokDirectPostIsPrivate()
    {
        using var media = new TestMedia();
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "creator_info/query/", "{\"data\":{\"privacy_level_options\":[\"SELF_ONLY\"]},\"error\":{\"code\":\"ok\"}}")
            .Json(HttpMethod.Post, "publish/video/init/", "{\"data\":{\"publish_id\":\"p2\",\"upload_url\":\"https://upload.example/u2\"},\"error\":{\"code\":\"ok\"}}")
            .Json(HttpMethod.Put, "upload.example/u2", "{}")
            .Json(HttpMethod.Post, "status/fetch/", "{\"data\":{\"status\":\"PUBLISH_COMPLETE\"},\"error\":{\"code\":\"ok\"}}");
        var publisher = new TikTokPublisher(http.Transport, NullLogger<TikTokPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var request = TestMedia.Request(post, "Caption", Credentials.Empty.With((CredentialKeys.AccessToken, "t")), options: new() { [TargetOptionKeys.TikTokMode] = "direct" });
        var result = await publisher.PublishAsync(request, null, None);

        result.State.ShouldBe(TargetStatus.Published);
        http.To("publish/video/init/").Single().Body.ShouldContain("\"privacy_level\":\"SELF_ONLY\"");
    }

    [Fact]
    public async Task TikTokPublicAccountErrorSaysTheAccountMustBePrivate()
    {
        using var media = new TestMedia();
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "creator_info/query/", "{\"data\":{\"privacy_level_options\":[\"SELF_ONLY\"]},\"error\":{\"code\":\"ok\"}}")
            .Json(HttpMethod.Post, "publish/video/init/", "{\"error\":{\"code\":\"unaudited_client_can_only_post_to_private_accounts\",\"message\":\"x\"}}",
                HttpStatusCode.Forbidden);
        var publisher = new TikTokPublisher(http.Transport, NullLogger<TikTokPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var request = TestMedia.Request(post, "Caption", Credentials.Empty.With((CredentialKeys.AccessToken, "t")), options: new() { [TargetOptionKeys.TikTokMode] = "direct" });
        var error = await Should.ThrowAsync<TMPublisher.Core.Publishing.PlatformRejectedException>(() => publisher.PublishAsync(request, null, None));
        error.Message.ShouldContain("private TikTok account");
    }

    [Theory]
    [InlineData("Yeni video (bölüm 2) – detaylar [link]", @"Yeni video \(bölüm 2\) – detaylar \[link\]")]
    [InlineData(@"my_file *bold* ~x~ a|b {c} <d> @e \f", @"my\_file \*bold\* \~x\~ a\|b \{c\} \<d\> \@e \\f")]
    [InlineData("#news today (#tag) C# 5 # alone", @"#news today \(#tag\) C\# 5 \# alone")]
    public void LinkedInTextIsEscapedAsLittleText(string text, string expected) => LittleText.Escape(text).ShouldBe(expected);

    [Fact]
    public void TikTokChunkPlanFollowsTheRules()
    {
        TikTokPublisher.ChunkPlan(3_000_000, 10_000_000).ShouldBe((3_000_000L, 1));
        TikTokPublisher.ChunkPlan(25_000_000, 10_000_000).ShouldBe((10_000_000L, 2));
    }

    [Fact]
    public async Task LinkedInPostsAsTheMemberAndReadsTheIdHeader()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Get, "v2/userinfo", "{\"sub\":\"abc\",\"name\":\"Example\"}")
            .On(HttpMethod.Post, "rest/posts", (request, _) =>
            {
                request.Headers.GetValues("LinkedIn-Version").Single().ShouldBe(LinkedInApi.Version);
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Headers.Add("x-restli-id", "urn:li:share:1");
                return response;
            });
        var platform = new LinkedInPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var connected = await platform.Connection.ConnectAsync(new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.AccessToken] = "tok" }), new FakePrompt(), None);
        connected.ReconnectByUtc.ShouldNotBeNull();

        var result = await platform.Publisher.PublishAsync(TestMedia.Request(new Post { Kind = PostKind.Text }, "Hello (part 1) #news", connected.Credentials), null, None);
        result.Url.ShouldBe("https://www.linkedin.com/feed/update/urn%3Ali%3Ashare%3A1/");
        var body = System.Text.Json.Nodes.JsonNode.Parse(http.To("rest/posts").Single().Body)!;
        body["author"]!.GetValue<string>().ShouldBe("urn:li:person:abc");
        body["commentary"]!.GetValue<string>().ShouldBe(@"Hello \(part 1\) #news");
    }

    [Fact]
    public async Task PinterestImagePinGoesAsBase64ToTheChosenBoard()
    {
        using var media = new TestMedia();
        var http = new FakeHttp().Json(HttpMethod.Post, "v5/pins", "{\"id\":\"pin1\"}");
        var publisher = new PinterestPublisher(http.Transport, NullLogger<PinterestPublisher>.Instance);
        var post = new Post { Kind = PostKind.Photo, Media = [media.Photo()] };
        var creds = Credentials.Empty.With((CredentialKeys.AccessToken, "t"), (PinterestApi.BoardKey, "b1"));
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Description", creds, title: "Title"), null, None);

        result.Url.ShouldBe("https://www.pinterest.com/pin/pin1/");
        var body = http.To("v5/pins").Single().Body;
        body.ShouldContain("\"board_id\":\"b1\"");
        body.ShouldContain("\"source_type\":\"image_base64\"");
    }

    [Fact]
    public async Task PinterestConnectChoosesTheBoardByName()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "v5/oauth/token", "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":2592000}")
            .Json(HttpMethod.Get, "v5/boards", "{\"items\":[{\"id\":\"b1\",\"name\":\"Ideas\"},{\"id\":\"b2\",\"name\":\"Videos\"}]}")
            .Json(HttpMethod.Get, "v5/user_account", "{\"username\":\"example\"}");
        var platform = new PinterestPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var prompt = new FakePrompt(opened => "https://localhost/callback?code=c&state=" + Uri.EscapeDataString(TMPublisher.Platforms.Connections.LoopbackListener.Parse(opened!.Query)["state"]));
        var input = new ConnectionInput(new Dictionary<string, string>
        {
            [CredentialKeys.ClientId] = "id",
            [CredentialKeys.ClientSecret] = "sec",
            [CredentialKeys.RedirectUri] = "https://localhost/callback",
            [PinterestApi.BoardKey] = "videos",
        });
        var result = await platform.Connection.ConnectAsync(input, prompt, None);
        result.Credentials[PinterestApi.BoardKey].ShouldBe("b2");
        http.To("v5/oauth/token").Single().Request.Headers.Authorization!.Scheme.ShouldBe("Basic");
    }
}
