using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Telegram;

namespace TMPublisher.Tests.Platforms;

public sealed class TelegramTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static Credentials Creds => Credentials.Empty.With((CredentialKeys.BotToken, "123:abc"), (CredentialKeys.ChatId, "@example"));

    [Fact]
    public async Task ConnectReadsBotAndChat()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Get, "/getMe", "{\"ok\":true,\"result\":{\"username\":\"example_bot\"}}")
            .Json(HttpMethod.Get, "/getChat", "{\"ok\":true,\"result\":{\"id\":-1001,\"title\":\"Example channel\",\"username\":\"example\"}}");
        var platform = new TelegramPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var result = await platform.Connection.ConnectAsync(new ConnectionInput(Creds.Values), new FakePrompt(), None);
        result.Identity.ExternalId.ShouldBe("-1001");
        result.Identity.DisplayName.ShouldBe("Example channel (@example_bot)");
        result.Credentials[CredentialKeys.BotToken].ShouldBe("123:abc");
    }

    [Fact]
    public async Task WrongTokenIsAnAuthError()
    {
        var http = new FakeHttp().Json(HttpMethod.Get, "/getMe", "{\"ok\":false,\"description\":\"Unauthorized\"}", HttpStatusCode.Unauthorized);
        var platform = new TelegramPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        await Should.ThrowAsync<PlatformAuthException>(() => platform.Connection.ConnectAsync(new ConnectionInput(Creds.Values), new FakePrompt(), None));
    }

    [Fact]
    public async Task TextGoesToSendMessage()
    {
        var http = new FakeHttp().Json(HttpMethod.Post, "/sendMessage", "{\"ok\":true,\"result\":{\"message_id\":7,\"chat\":{\"username\":\"example\"}}}");
        var publisher = new TelegramPublisher(http.Transport, NullLogger<TelegramPublisher>.Instance);
        var result = await publisher.PublishAsync(TestMedia.Request(new Post { Kind = PostKind.Text }, "Hello", Creds), null, None);
        result.Url.ShouldBe("https://t.me/example/7");
        http.Requests.Single().Body.ShouldContain("\"text\":\"Hello\"");
    }

    [Fact]
    public async Task SeveralFilesGoToSendMediaGroupWithCaptionOnTheFirst()
    {
        using var media = new TestMedia();
        var http = new FakeHttp().Json(HttpMethod.Post, "/sendMediaGroup", "{\"ok\":true,\"result\":[{\"message_id\":9,\"chat\":{}}]}");
        var publisher = new TelegramPublisher(http.Transport, NullLogger<TelegramPublisher>.Instance);
        var post = new Post { Kind = PostKind.Album, Media = [media.Photo(), media.Video()] };
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Caption", Creds), null, None);
        result.ExternalId.ShouldBe("9");
        result.Url.ShouldBeEmpty();
        var body = http.Requests.Single().Body;
        body.ShouldContain("attach://file0");
        body.ShouldContain("\"type\":\"video\"");
        body.ShouldContain("Caption");
    }

    [Fact]
    public async Task MissingFileStopsBeforeAnyRequest()
    {
        var http = new FakeHttp();
        var publisher = new TelegramPublisher(http.Transport, NullLogger<TelegramPublisher>.Instance);
        var post = new Post { Kind = PostKind.Photo, Media = [new MediaItem("does-not-exist.jpg", MediaType.Photo, 1)] };
        await Should.ThrowAsync<PlatformRejectedException>(() => publisher.PublishAsync(TestMedia.Request(post, "x", Creds), null, None));
        http.Requests.ShouldBeEmpty();
    }
}
