using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Discord;

namespace TMPublisher.Tests.Platforms;

public sealed class DiscordTests
{
    private const string Hook = "https://discord.com/api/webhooks/123/abcDEF";

    [Fact]
    public async Task OnlyWebhookAddressesAreAccepted()
    {
        var http = new FakeHttp().Json(HttpMethod.Get, "/webhooks/123/", "{\"id\":\"123\",\"name\":\"Example hook\",\"channel_id\":\"55\"}");
        var platform = new DiscordPlatform(http.Transport, TimeProvider.System, NullLoggerFactory.Instance);
        var bad = new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.WebhookUrl] = "https://example.com/hook" });
        await Should.ThrowAsync<PlatformRejectedException>(() => platform.Connection.ConnectAsync(bad, new FakePrompt(), CancellationToken.None));

        var good = new ConnectionInput(new Dictionary<string, string> { [CredentialKeys.WebhookUrl] = Hook });
        var result = await platform.Connection.ConnectAsync(good, new FakePrompt(), CancellationToken.None);
        result.Identity.DisplayName.ShouldBe("Example hook");
    }

    [Fact]
    public async Task FilesGoAsMultipartAndTheAddressIsBuilt()
    {
        using var media = new TestMedia();
        // A REST message carries no guild_id; the server id comes from the webhook.
        var http = new FakeHttp()
            .Json(HttpMethod.Post, "?wait=true", "{\"id\":\"9\",\"channel_id\":\"55\"}")
            .Json(HttpMethod.Get, "/webhooks/123/", "{\"id\":\"123\",\"channel_id\":\"55\",\"guild_id\":\"77\"}");
        var publisher = new DiscordPublisher(http.Transport, NullLogger<DiscordPublisher>.Instance);
        var post = new Post { Kind = PostKind.Photo, Media = [media.Photo()] };
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Hi", Credentials.Empty.With((CredentialKeys.WebhookUrl, Hook))), null, CancellationToken.None);
        result.Url.ShouldBe("https://discord.com/channels/77/55/9");
        http.To("?wait=true").Single().Body.ShouldContain("payload_json");
        http.To("?wait=true").Single().Body.ShouldContain("files[0]");
    }

    [Fact]
    public async Task APostStaysPublishedWhenTheServerIdCannotBeRead()
    {
        var http = new FakeHttp().Json(HttpMethod.Post, "?wait=true", "{\"id\":\"9\",\"channel_id\":\"55\"}");
        var publisher = new DiscordPublisher(http.Transport, NullLogger<DiscordPublisher>.Instance);
        var post = new Post { Kind = PostKind.Text };
        var result = await publisher.PublishAsync(TestMedia.Request(post, "Hi", Credentials.Empty.With((CredentialKeys.WebhookUrl, Hook))), null, CancellationToken.None);
        result.State.ShouldBe(TargetStatus.Published);
        result.ExternalId.ShouldBe("9");
        result.Url.ShouldBeEmpty();
    }
}
