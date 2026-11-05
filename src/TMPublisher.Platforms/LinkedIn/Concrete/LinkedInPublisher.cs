using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.LinkedIn;

/// <summary>
/// LinkedIn Posts API (REST, versioned): images → <c>/rest/images?action=initializeUpload</c> + PUT; video → <c>/rest/videos?action=
/// initializeUpload</c>, PUT every part (ETags collected), <c>finalizeUpload</c>; then <c>POST /rest/posts</c> as the member
/// (<c>urn:li:person:{id}</c>, public, main feed). The text is sent in LinkedIn's "little" format (<see cref="LittleText"/>). The new
/// post's id comes in the <c>x-restli-id</c> header.
/// </summary>
public sealed class LinkedInPublisher(HttpTransport transport, ILogger<LinkedInPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = Session(request);
        var author = "urn:li:person:" + request.Credentials.Require(CredentialKeys.UserId);
        var post = request.Post;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Video ? 500L * 1024 * 1024 : 0, "LinkedIn");
        }

        var urns = new List<string>();
        for (var i = 0; i < post.Media.Count; i++)
        {
            urns.Add(post.Media[i].Type == MediaType.Photo
                ? await UploadImageAsync(post.Media[i].Path, author, session, cancellationToken).ConfigureAwait(false)
                : await UploadVideoAsync(post.Media[i].Path, author, session, cancellationToken).ConfigureAwait(false));
            progress?.Report(0.1 + (0.7 * (i + 1) / post.Media.Count));
        }

        var body = new JsonObject
        {
            ["author"] = author,
            ["commentary"] = LittleText.Escape(request.Target.Text),
            ["visibility"] = "PUBLIC",
            ["distribution"] = new JsonObject { ["feedDistribution"] = "MAIN_FEED", ["targetEntities"] = new JsonArray(), ["thirdPartyDistributionChannels"] = new JsonArray() },
            ["lifecycleState"] = "PUBLISHED",
            ["isReshareDisabledByAuthor"] = false,
        };
        if (urns.Count == 1)
        {
            body["content"] = new JsonObject { ["media"] = new JsonObject { ["id"] = urns[0] } };
        }
        else if (urns.Count > 1)
        {
            body["content"] = new JsonObject { ["multiImage"] = new JsonObject { ["images"] = new JsonArray(urns.Select(u => (JsonNode)new JsonObject { ["id"] = u }).ToArray()) } };
        }

        var reply = await SendAsync(() => Rest(HttpMethod.Post, "posts", body), session, null, cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            throw Classify("post", reply.Status, reply.Body);
        }

        var id = reply.Headers?.GetValueOrDefault("x-restli-id") ?? string.Empty;
        progress?.Report(1);
        Logger.LogInformation("LinkedIn post created");
        return new PublishResult(id, id.Length > 0 ? "https://www.linkedin.com/feed/update/" + Uri.EscapeDataString(id) + "/" : string.Empty, TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var text = $"LinkedIn {step} failed (HTTP {status}): {(JsonSafe.Str(JsonSafe.TryParse(body), "message") is { Length: > 0 } m ? m : PlatformText.Short(body))}";
        return status switch
        {
            401 => new PlatformAuthException(text + " The token expired (60 days); connect the account again."),
            429 => new PlatformQuotaException(text),
            _ => new PlatformRejectedException(text),
        };
    }

    private static HttpRequestMessage Rest(HttpMethod method, string path, JsonNode body)
    {
        var message = new HttpRequestMessage(method, LinkedInApi.Rest + path)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("LinkedIn-Version", LinkedInApi.Version);
        message.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
        return message;
    }

    private async Task<string> UploadImageAsync(string path, string author, TokenSession session, CancellationToken cancellationToken)
    {
        var init = await SendJsonAsync(() => Rest(HttpMethod.Post, "images?action=initializeUpload",
            new JsonObject { ["initializeUploadRequest"] = new JsonObject { ["owner"] = author } }), session, "image initialize", cancellationToken).ConfigureAwait(false);
        var upload = JsonSafe.Str(init?["value"], "uploadUrl");
        var urn = JsonSafe.Str(init?["value"], "image");
        await PutAsync(upload, File.ReadAllBytes(path), session, "image upload", cancellationToken).ConfigureAwait(false);
        return urn;
    }

    private async Task<string> UploadVideoAsync(string path, string author, TokenSession session, CancellationToken cancellationToken)
    {
        var size = new FileInfo(path).Length;
        var init = await SendJsonAsync(() => Rest(HttpMethod.Post, "videos?action=initializeUpload", new JsonObject
        {
            ["initializeUploadRequest"] = new JsonObject { ["owner"] = author, ["fileSizeBytes"] = size, ["uploadCaptions"] = false, ["uploadThumbnail"] = false },
        }), session, "video initialize", cancellationToken).ConfigureAwait(false);
        var value = init?["value"];
        var urn = JsonSafe.Str(value, "video");
        var etags = new JsonArray();
        await using (var stream = File.OpenRead(path))
        {
            foreach (var instruction in value?["uploadInstructions"] as JsonArray ?? [])
            {
                var first = long.Parse(JsonSafe.Str(instruction, "firstByte"), System.Globalization.CultureInfo.InvariantCulture);
                var last = long.Parse(JsonSafe.Str(instruction, "lastByte"), System.Globalization.CultureInfo.InvariantCulture);
                var bytes = new byte[last - first + 1];
                stream.Position = first;
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                etags.Add((JsonNode?)JsonValue.Create(await PutAsync(JsonSafe.Str(instruction, "uploadUrl"), bytes, session, "video upload", cancellationToken).ConfigureAwait(false)));
            }
        }

        await SendJsonAsync(() => Rest(HttpMethod.Post, "videos?action=finalizeUpload", new JsonObject
        {
            ["finalizeUploadRequest"] = new JsonObject { ["video"] = urn, ["uploadToken"] = JsonSafe.Str(value, "uploadToken"), ["uploadedPartIds"] = etags },
        }), session, "video finalize", cancellationToken).ConfigureAwait(false);
        return urn;
    }

    private async Task<string> PutAsync(string address, byte[] bytes, TokenSession session, string step, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, address) { Content = new ByteArrayContent(bytes) };
        Authorize(message, session.Token);
        using var response = await Http.Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw Classify(step, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }

        return response.Headers.ETag?.Tag ?? (response.Headers.TryGetValues("etag", out var tags) ? tags.First() : string.Empty);
    }
}
