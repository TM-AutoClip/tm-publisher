using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Core.Text;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.X;

/// <summary>
/// X API v2 (ported from TM AutoClip; photos added). Media, chunked:
/// <list type="number">
/// <item><c>POST /2/media/upload/initialize</c> (media_type, total_bytes, media_category tweet_image / tweet_video) → id.</item>
/// <item><c>POST /2/media/upload/{id}/append</c> multipart segment_index + media (4 MB parts).</item>
/// <item><c>POST /2/media/upload/{id}/finalize</c>; a video's <c>processing_info</c> is polled until succeeded (at most 10 minutes); a post is
/// never sent before the media is ready.</item>
/// <item><c>POST /2/tweets</c> with text and media ids (<c>made_with_ai</c> when the post is marked synthetic). Address x.com/i/status/{id}.</item>
/// </list>
/// The 280 weighted characters are checked before any request (never cut). 401 → the token is renewed once; 402 credits; 429 limit.
/// </summary>
public sealed class XPublisher(HttpTransport transport, ILogger<XPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    /// <summary>API root.</summary>
    public const string Api = "https://api.x.com/2/";

    private static readonly TimeSpan ProcessingLimit = TimeSpan.FromMinutes(10);

    /// <summary>Part size (smaller in tests).</summary>
    internal int ChunkBytes { get; set; } = 4 * 1024 * 1024;

    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Target.Text;
        var weight = XText.WeightedLength(text);
        if (weight > XText.MaxWeight)
        {
            throw new PlatformRejectedException($"The X text is {weight} weighted characters; the limit is {XText.MaxWeight}.");
        }

        var post = request.Post;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Photo ? 5L * 1024 * 1024 : 512L * 1024 * 1024, "X");
        }

        var session = Session(request);
        var ids = new JsonArray();
        for (var i = 0; i < post.Media.Count; i++)
        {
            ids.Add(await UploadAsync(post.Media[i], session, cancellationToken).ConfigureAwait(false));
            progress?.Report(0.1 + (0.75 * (i + 1) / post.Media.Count));
        }

        var body = new JsonObject { ["text"] = text };
        if (ids.Count > 0)
        {
            body["media"] = new JsonObject { ["media_ids"] = ids };
        }

        if (Flag(request.Target, TargetOptionKeys.Synthetic))
        {
            body["made_with_ai"] = true;
        }

        var created = await SendJsonAsync(() => Json(Api + "tweets", body), session, "post", cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(created?["data"], "id");
        if (id.Length == 0)
        {
            throw new PlatformRejectedException("X returned no post id.");
        }

        progress?.Report(1);
        Logger.LogInformation("X post {PostId} created", id);
        return new PublishResult(id, "https://x.com/i/status/" + Uri.EscapeDataString(id), TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var root = JsonSafe.TryParse(body);
        var parts = new[] { JsonSafe.Str(root, "title"), JsonSafe.Str(root, "detail") }.Where(s => s.Length > 0).ToList();
        var message = parts.Count > 0 ? string.Join(" · ", parts) : JsonSafe.Str(root?["errors"]?[0], "message") is { Length: > 0 } first ? first : PlatformText.Short(body);
        var text = $"X {step} failed (HTTP {status}): {message}";
        return status switch
        {
            401 => new PlatformAuthException(text),
            429 => new PlatformQuotaException(text),
            402 => new PlatformRejectedException(text + " (the X developer account may be out of credits)"),
            _ => new PlatformRejectedException(text),
        };
    }

    private async Task<string> UploadAsync(MediaItem media, TokenSession session, CancellationToken cancellationToken)
    {
        var length = new FileInfo(media.Path).Length;
        var init = new JsonObject
        {
            ["media_type"] = MediaTypes.Of(media.Path),
            ["total_bytes"] = length,
            ["media_category"] = media.Type == MediaType.Photo ? "tweet_image" : "tweet_video",
        };
        var started = await SendJsonAsync(() => Json(Api + "media/upload/initialize", init), session, "media initialize", cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(started?["data"], "id");
        if (id.Length == 0)
        {
            throw new PlatformRejectedException("X returned no media id.");
        }

        await using (var stream = File.OpenRead(media.Path))
        {
            var buffer = new byte[ChunkBytes];
            for (var index = 0; ; index++)
            {
                var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var segment = index;
                var bytes = buffer.AsMemory(0, read).ToArray();
                await SendJsonAsync(() =>
                {
                    var part = new ByteArrayContent(bytes);
                    part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    var form = new MultipartFormDataContent { { new StringContent(segment.ToString(CultureInfo.InvariantCulture)), "segment_index" }, { part, "media", "chunk" + segment.ToString(CultureInfo.InvariantCulture) } };
                    return new HttpRequestMessage(HttpMethod.Post, Api + "media/upload/" + Uri.EscapeDataString(id) + "/append") { Content = form };
                }, session, "media append", cancellationToken).ConfigureAwait(false);
            }
        }

        var finalized = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, Api + "media/upload/" + Uri.EscapeDataString(id) + "/finalize"),
            session, "media finalize", cancellationToken).ConfigureAwait(false);
        var info = finalized?["data"]?["processing_info"];
        await PollAsync<bool>(async (round, ct) =>
        {
            if (round > 0)
            {
                var status = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, Api + "media/upload?command=STATUS&media_id=" + Uri.EscapeDataString(id)),
                    session, "media status", ct).ConfigureAwait(false);
                info = status?["data"]?["processing_info"];
            }

            var state = JsonSafe.Str(info, "state");
            if (info is null || state == "succeeded")
            {
                return PollStep<bool>.Done(true);
            }

            if (state == "failed")
            {
                throw new PlatformRejectedException("X could not process the media: " + JsonSafe.Str(info["error"], "message"));
            }

            var after = int.TryParse(JsonSafe.Str(info, "check_after_secs"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 5;
            return PollStep<bool>.Again(TimeSpan.FromSeconds(Math.Clamp(after, 1, 30)));
        }, TimeSpan.Zero, ProcessingLimit, () => new PlatformRejectedException("X did not process the media in 10 minutes."), cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static HttpRequestMessage Json(string address, JsonObject body) =>
        new(HttpMethod.Post, address) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
