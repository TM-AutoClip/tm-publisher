using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Meta;
using TMPublisher.Platforms.Threads;

namespace TMPublisher.Tests.Platforms;

public sealed class MetaTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private const string TwoPages = "{\"data\":[{\"id\":\"111\",\"name\":\"Page one\",\"access_token\":\"pt1\"},"
        + "{\"id\":\"222\",\"name\":\"Page two\",\"access_token\":\"pt2\",\"instagram_business_account\":{\"id\":\"ig2\",\"username\":\"example\"}}]}";

    private static ConnectionInput Input(string pageId = "") => new(new Dictionary<string, string>
    {
        [CredentialKeys.ClientId] = "app",
        [CredentialKeys.ClientSecret] = "secret",
        [CredentialKeys.UserToken] = "short",
        [CredentialKeys.PageId] = pageId,
    });

    private static FakeHttp MetaHttp() => new FakeHttp()
        .Json(HttpMethod.Get, "oauth/access_token?grant_type=fb_exchange_token", "{\"access_token\":\"long\",\"expires_in\":5183944}")
        .Json(HttpMethod.Get, "me/accounts", TwoPages)
        .Json(HttpMethod.Get, "/222?fields=id,name,link", "{\"id\":\"222\",\"name\":\"Page two\",\"link\":\"https://www.facebook.com/example\"}")
        .Json(HttpMethod.Get, "/ig2?fields=id,username,name", "{\"id\":\"ig2\",\"username\":\"example\"}");

    [Fact]
    public async Task FacebookAsksForThePageWhenTheTokenSeesSeveral()
    {
        var platform = new FacebookPlatform(MetaHttp().Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var error = await Should.ThrowAsync<PlatformRejectedException>(() => platform.Connection.ConnectAsync(Input(), new FakePrompt(), None));
        error.Message.ShouldContain("Page two (222)");

        var result = await platform.Connection.ConnectAsync(Input("222"), new FakePrompt(), None);
        result.Identity.DisplayName.ShouldBe("Page two");
        result.Credentials[CredentialKeys.AccessToken].ShouldBe("pt2");
        result.Credentials.Values.ShouldNotContainKey(CredentialKeys.UserToken);
    }

    [Fact]
    public async Task InstagramNeedsALinkedProfessionalAccount()
    {
        var platform = new InstagramPlatform(MetaHttp().Transport, TimeProvider.System, NullLoggerFactory.Instance);
        await Should.ThrowAsync<PlatformRejectedException>(() => platform.Connection.ConnectAsync(Input("111"), new FakePrompt(), None));
        var result = await platform.Connection.ConnectAsync(Input("222"), new FakePrompt(), None);
        result.Identity.Handle.ShouldBe("@example");
        result.Credentials[MetaKeys.InstagramId].ShouldBe("ig2");
    }

    [Fact]
    public async Task FacebookMultiPhotoUploadsUnpublishedThenAttaches()
    {
        using var media = new TestMedia();
        var photo = 0;
        var http = new FakeHttp()
            .On(HttpMethod.Post, "/222/photos", (_, _) => FakeHttp.Response(HttpStatusCode.OK, "{\"id\":\"ph" + ++photo + "\"}"))
            .Json(HttpMethod.Post, "/222/feed", "{\"id\":\"222_9\"}");
        var publisher = new FacebookPublisher(http.Transport, NullLogger<FacebookPublisher>.Instance);
        var post = new Post { Kind = PostKind.Photo, Media = [media.Photo("a.jpg"), media.Photo("b.jpg")] };
        var creds = Credentials.Empty.With((CredentialKeys.PageId, "222"), (CredentialKeys.AccessToken, "pt2"));
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Hello", creds), null, None);

        result.Url.ShouldBe("https://www.facebook.com/222_9");
        http.To("/222/photos").All(r => r.Body.Contains("false", StringComparison.Ordinal)).ShouldBeTrue();
        var feed = Uri.UnescapeDataString(http.To("/222/feed").Single().Body);
        feed.ShouldContain("attached_media[0]={\"media_fbid\":\"ph1\"}");
        feed.ShouldContain("attached_media[1]={\"media_fbid\":\"ph2\"}");
    }

    [Fact]
    public async Task InstagramReelUsesResumableUploadWithTheOAuthHeader()
    {
        using var media = new TestMedia();
        var polls = 0;
        var http = new FakeHttp()
            .Json(HttpMethod.Get, "content_publishing_limit", "{\"data\":[{\"quota_usage\":3,\"config\":{\"quota_total\":100}}]}")
            .Json(HttpMethod.Post, "/ig2/media_publish", "{\"id\":\"m9\"}")
            .Json(HttpMethod.Post, "/ig2/media", "{\"id\":\"c1\",\"uri\":\"https://rupload.facebook.com/ig-api-upload/v26.0/c1\"}")
            .Json(HttpMethod.Post, "rupload.facebook.com", "{\"success\":true}")
            .On(HttpMethod.Get, "/c1?fields=status_code", (_, _) => FakeHttp.Response(HttpStatusCode.OK,
                polls++ == 0 ? "{\"status_code\":\"IN_PROGRESS\"}" : "{\"status_code\":\"FINISHED\"}"))
            .Json(HttpMethod.Get, "/m9?fields=permalink", "{\"permalink\":\"https://www.instagram.com/reel/abc/\"}");
        var publisher = new InstagramPublisher(http.Transport, NullLogger<InstagramPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var creds = Credentials.Empty.With((MetaKeys.InstagramId, "ig2"), (CredentialKeys.AccessToken, "pt2"));
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Caption #a", creds), null, None);

        result.Url.ShouldBe("https://www.instagram.com/reel/abc/");
        http.To("/ig2/media").First().Body.ShouldContain("upload_type=resumable");
        var upload = http.To("rupload.facebook.com").Single().Request;
        string.Join(",", upload.Headers.GetValues("Authorization")).ShouldBe("OAuth pt2");
        upload.Headers.GetValues("file_size").Single().ShouldBe("5000");
    }

    public static TheoryData<string> PermalinkFailures => ["quota", "auth", "network"];

    [Theory]
    [MemberData(nameof(PermalinkFailures))]
    public async Task InstagramReelStaysPublishedWhenItsAddressCannotBeRead(string failure)
    {
        using var media = new TestMedia();
        var http = new FakeHttp()
            .Json(HttpMethod.Get, "content_publishing_limit", "{\"data\":[]}")
            .Json(HttpMethod.Post, "/ig2/media_publish", "{\"id\":\"m9\"}")
            .Json(HttpMethod.Post, "/ig2/media", "{\"id\":\"c1\",\"uri\":\"https://rupload.facebook.com/ig-api-upload/v26.0/c1\"}")
            .Json(HttpMethod.Post, "rupload.facebook.com", "{\"success\":true}")
            .Json(HttpMethod.Get, "/c1?fields=status_code", "{\"status_code\":\"FINISHED\"}")
            .On(HttpMethod.Get, "/m9?fields=permalink", (_, _) => Failure(failure));
        var publisher = new InstagramPublisher(http.Transport, NullLogger<InstagramPublisher>.Instance) { Delay = (_, _) => Task.CompletedTask };
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var creds = Credentials.Empty.With((MetaKeys.InstagramId, "ig2"), (CredentialKeys.AccessToken, "pt2"));
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Caption", creds), null, None);

        result.State.ShouldBe(TargetStatus.Published);
        result.ExternalId.ShouldBe("m9");
        result.Url.ShouldBeEmpty();
        http.To("media_publish").Count().ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(PermalinkFailures))]
    public async Task ThreadsPostStaysPublishedWhenItsAddressCannotBeRead(string failure)
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "/77/threads_publish", "{\"id\":\"p1\"}")
            .Json(HttpMethod.Post, "/77/threads", "{\"id\":\"c1\"}")
            .Json(HttpMethod.Get, "/c1?fields=status", "{\"status\":\"FINISHED\"}")
            .On(HttpMethod.Get, "/p1?fields=permalink", (_, _) => Failure(failure));
        var publisher = new ThreadsPublisher(http.Transport, NullLogger<ThreadsPublisher>.Instance);
        var creds = Credentials.Empty.With((CredentialKeys.UserId, "77"), (CredentialKeys.AccessToken, "long"));
        var result = await publisher.PublishAsync(TestMedia.Request(new Post { Kind = PostKind.Text }, "Hello", creds), null, None);
        result.State.ShouldBe(TargetStatus.Published);
        result.ExternalId.ShouldBe("p1");
        result.Url.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnExpiredThreadsTokenMeansConnectAgain()
    {
        var http = new FakeHttp().Json(HttpMethod.Get, "refresh_access_token",
            "{\"error\":{\"message\":\"Error validating access token\",\"type\":\"OAuthException\",\"code\":190}}", HttpStatusCode.BadRequest);
        var refresher = new ThreadsTokenRefresher(http.Transport, TimeProvider.System);
        var creds = Credentials.Empty.With((CredentialKeys.AccessToken, "old"));
        await Should.ThrowAsync<PlatformAuthException>(() => refresher.RefreshAsync(creds, None));
    }

    private static HttpResponseMessage Failure(string kind) => kind switch
    {
        "quota" => FakeHttp.Response(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"limit\",\"code\":4}}"),
        "auth" => FakeHttp.Response(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"expired\",\"code\":190}}"),
        _ => throw new HttpRequestException("The network went away"),
    };

    [Fact]
    public async Task InstagramStopsAtTheDailyLimitBeforeUploading()
    {
        using var media = new TestMedia();
        var http = new FakeHttp().Json(HttpMethod.Get, "content_publishing_limit", "{\"data\":[{\"quota_usage\":100,\"config\":{\"quota_total\":100}}]}");
        var publisher = new InstagramPublisher(http.Transport, NullLogger<InstagramPublisher>.Instance);
        var post = new Post { Kind = PostKind.Video, Media = [media.Video()] };
        var creds = Credentials.Empty.With((MetaKeys.InstagramId, "ig2"), (CredentialKeys.AccessToken, "pt2"));
        await Should.ThrowAsync<PlatformQuotaException>(() => publisher.PublishAsync(TestMedia.Request(post, "c", creds), null, None));
        http.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ThreadsConnectsFromThePastedAddressAndPostsText()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "oauth/access_token", "{\"access_token\":\"short\",\"user_id\":\"77\"}")
            .Json(HttpMethod.Get, "access_token?grant_type=th_exchange_token", "{\"access_token\":\"long\",\"expires_in\":5183944}")
            .Json(HttpMethod.Get, "me?fields=id,username,name", "{\"id\":\"77\",\"username\":\"example\"}");
        var platform = new ThreadsPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var prompt = new FakePrompt(opened =>
            "https://localhost/callback?state=" + Uri.EscapeDataString(TMPublisher.Platforms.Connections.LoopbackListener.Parse(opened!.Query)["state"]) + "&code=abc#_");
        var input = new ConnectionInput(new Dictionary<string, string>
        {
            [CredentialKeys.ClientId] = "app",
            [CredentialKeys.ClientSecret] = "secret",
            [CredentialKeys.RedirectUri] = "https://localhost/callback",
        });
        var result = await platform.Connection.ConnectAsync(input, prompt, None);
        result.Credentials[CredentialKeys.AccessToken].ShouldBe("long");
        result.Credentials[CredentialKeys.UserId].ShouldBe("77");
        platform.Refresher!.NeedsRefresh(result.Credentials, DateTimeOffset.UtcNow).ShouldBeFalse();
        platform.Refresher.NeedsRefresh(result.Credentials, DateTimeOffset.UtcNow.AddDays(55)).ShouldBeTrue();
    }

    [Fact]
    public async Task ThreadsPublishWaitsForTheContainer()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "/77/threads_publish", "{\"id\":\"p1\"}")
            .Json(HttpMethod.Post, "/77/threads", "{\"id\":\"c1\"}")
            .Json(HttpMethod.Get, "/c1?fields=status", "{\"status\":\"FINISHED\"}")
            .Json(HttpMethod.Get, "/p1?fields=permalink", "{\"permalink\":\"https://www.threads.net/@example/post/p1\"}");
        var publisher = new ThreadsPublisher(http.Transport, NullLogger<ThreadsPublisher>.Instance);
        var creds = Credentials.Empty.With((CredentialKeys.UserId, "77"), (CredentialKeys.AccessToken, "long"));
        var result = await publisher.PublishAsync(TestMedia.Request(new Post { Kind = PostKind.Text }, "Hello", creds), null, None);
        result.Url.ShouldBe("https://www.threads.net/@example/post/p1");
        http.To("/77/threads").First().Body.ShouldContain("media_type=TEXT");
    }
}
