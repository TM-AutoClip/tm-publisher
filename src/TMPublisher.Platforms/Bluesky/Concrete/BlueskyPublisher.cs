using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Bluesky;

/// <summary>
/// Bluesky: media → <c>com.atproto.repo.uploadBlob</c> (bytes from this computer), then <c>com.atproto.repo.createRecord</c> with an
/// <c>app.bsky.feed.post</c> record: text, facets (links, hashtags), images embed (≤4) or video embed (1). The address is
/// <c>bsky.app/profile/{handle}/post/{rkey}</c>. An expired session is renewed once (refresh, or a new session with the app password).
/// </summary>
public sealed class BlueskyPublisher(HttpTransport transport, ILogger<BlueskyPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(15), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var server = BlueskySessions.Server(request.Credentials);
        var did = request.Credentials.Require(CredentialKeys.UserId);
        var session = Session(request);
        var post = request.Post;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Photo ? 2L * 1024 * 1024 : 50L * 1024 * 1024, "Bluesky");
        }

        var blobs = new List<JsonNode>();
        for (var i = 0; i < post.Media.Count; i++)
        {
            var path = post.Media[i].Path;
            var answer = await SendJsonAsync(() =>
            {
                var content = new StreamContent(File.OpenRead(path));
                content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.Of(path));
                return new HttpRequestMessage(HttpMethod.Post, server + "/xrpc/com.atproto.repo.uploadBlob") { Content = content };
            }, session, "uploadBlob", cancellationToken).ConfigureAwait(false);
            blobs.Add(answer?["blob"]?.DeepClone() ?? throw new PlatformRejectedException("Bluesky returned no blob for " + Path.GetFileName(path)));
            progress?.Report(0.1 + (0.7 * (i + 1) / post.Media.Count));
        }

        var text = request.Target.Text;
        var record = new JsonObject
        {
            ["$type"] = "app.bsky.feed.post",
            ["text"] = text,
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
        };
        var facets = BlueskyFacets.For(text);
        if (facets.Count > 0)
        {
            record["facets"] = facets;
        }

        if (post.Kind == PostKind.Photo)
        {
            record["embed"] = new JsonObject
            {
                ["$type"] = "app.bsky.embed.images",
                ["images"] = new JsonArray(blobs.Select(b => (JsonNode)new JsonObject { ["alt"] = string.Empty, ["image"] = b }).ToArray()),
            };
        }
        else if (post.Kind == PostKind.Video)
        {
            record["embed"] = new JsonObject { ["$type"] = "app.bsky.embed.video", ["video"] = blobs.Single() };
        }

        var body = new JsonObject { ["repo"] = did, ["collection"] = "app.bsky.feed.post", ["record"] = record };
        var created = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, server + "/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        }, session, "createRecord", cancellationToken).ConfigureAwait(false);
        var uri = JsonSafe.Str(created, "uri");
        var rkey = uri[(uri.LastIndexOf('/') + 1)..];
        var handle = request.Account.Handle?.TrimStart('@') ?? did;
        progress?.Report(1);
        Logger.LogInformation("Bluesky post {Rkey} created", rkey);
        return new PublishResult(uri, $"https://bsky.app/profile/{handle}/post/{rkey}", TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override bool IsTokenRejected(PlatformReply reply) =>
        reply.Status == 401 || (reply.Status == 400 && JsonSafe.Str(JsonSafe.TryParse(reply.Body), "error") is "ExpiredToken" or "InvalidToken");

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var root = JsonSafe.TryParse(body);
        var text = $"Bluesky {step} failed (HTTP {status}, {JsonSafe.Str(root, "error")}): {(JsonSafe.Str(root, "message") is { Length: > 0 } m ? m : PlatformText.Short(body))}";
        return status switch
        {
            401 => new PlatformAuthException(text),
            400 when JsonSafe.Str(root, "error") is "ExpiredToken" or "InvalidToken" => new PlatformAuthException(text),
            429 => new PlatformQuotaException(text),
            _ => new PlatformRejectedException(text),
        };
    }
}
