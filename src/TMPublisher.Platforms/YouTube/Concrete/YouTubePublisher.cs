using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.YouTube;

/// <summary>
/// YouTube <c>videos.insert</c> with a RESUMABLE upload (ported from TM AutoClip; the protocol unchanged). The server is asked "how far did
/// you get" and the upload continues from the remaining bytes, so a broken connection does not waste the whole upload.
/// <list type="bullet">
/// <item>Session: <c>POST upload/youtube/v3/videos?uploadType=resumable&amp;part=snippet,status</c> with size and type in headers; the
/// address comes in <c>Location</c>. It carries an authorization: never logged or put in an error message.</item>
/// <item>Chunks of 8 MB (a multiple of 256 KB, except the last). 308 = chunk received (<c>Range</c> tells how far); 200/201 = done.</item>
/// <item>Network break, 5xx or 429: back off, ask <c>bytes */size</c> for the resume point; 404/410: session lost, once from the start.</item>
/// <item>403 <c>quotaExceeded</c> / <c>uploadLimitExceeded</c> → <see cref="PlatformQuotaException"/> (resets at midnight Pacific Time);
/// 401 → the token is renewed once.</item>
/// <item>After the upload the item is "processing"; <see cref="CheckProcessingAsync"/> asks <c>videos.list</c> (a 200 does not mean the
/// video is live; YouTube may still reject it while processing).</item>
/// </list>
/// </summary>
public sealed class YouTubePublisher : PublisherBase
{
    /// <summary>Data API root.</summary>
    public const string Api = "https://www.googleapis.com/youtube/v3/";

    /// <summary>Upload root.</summary>
    public const string UploadApi = "https://www.googleapis.com/upload/youtube/v3/";

    /// <summary>Chunk size (8 MB = 32 × 256 KB).</summary>
    public const int ChunkSize = 8 * 1024 * 1024;

    /// <summary>Attempts on temporary errors.</summary>
    public const int MaxAttempts = 3;

    private readonly HttpClient _chunks;

    /// <summary>Creates.</summary>
    public YouTubePublisher(HttpTransport transport, ILogger<YouTubePublisher> logger)
        : base(transport, TimeSpan.FromSeconds(90), logger)
    {
        // A separate client for chunks: 8 MB on a slow line can take minutes.
        _chunks = Transport.CreateClient(TimeSpan.FromMinutes(15));
    }

    /// <summary>Chunk size (smaller in tests).</summary>
    internal int ChunkBytes { get; set; } = ChunkSize;

    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = CheckFile(request.Post.Media.Single(), 256L * 1024 * 1024 * 1024, "YouTube");
        var auth = Session(request);
        var type = MediaTypes.Of(file.FullName);
        Logger.LogInformation("YouTube upload starting: {Megabytes:0.#} MB, {Type}", file.Length / 1048576.0, type);
        var videoId = string.Empty;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var sessionUrl = await OpenSessionAsync(request, auth, file.Length, type, cancellationToken).ConfigureAwait(false);
            try
            {
                videoId = await SendChunksAsync(sessionUrl, file, type, auth, progress, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (UploadSessionLostException ex) when (attempt == 1)
            {
                Logger.LogWarning("YouTube upload session lost ({Reason}); starting a new session", ex.Message);
                progress?.Report(0);
            }
        }

        Logger.LogInformation("YouTube uploaded video {VideoId}", videoId);
        return new PublishResult(videoId, "https://youtu.be/" + Uri.EscapeDataString(videoId), TargetStatus.Processing);
    }

    /// <inheritdoc />
    public override async Task<ProcessingCheck> CheckProcessingAsync(PublishRequest request, string externalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Api + "videos?part=status,processingDetails&id=" + Uri.EscapeDataString(externalId)),
            Session(request), null, cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            throw Classify("processing check", reply.Status, reply.Body);
        }

        using var json = JsonDocument.Parse(reply.Body);
        if (!json.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            return new ProcessingCheck(TargetStatus.Failed, Message: "YouTube does not know the video (deleted or never created).");
        }

        var video = items[0];
        var upload = video.TryGetProperty("status", out var s) && s.TryGetProperty("uploadStatus", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        var processing = video.TryGetProperty("processingDetails", out var p) && p.TryGetProperty("processingStatus", out var ps) ? ps.GetString() ?? string.Empty : string.Empty;
        var reason = video.TryGetProperty("status", out var s2) && s2.TryGetProperty("rejectionReason", out var rr) ? " · " + rr.GetString()
            : video.TryGetProperty("status", out var s3) && s3.TryGetProperty("failureReason", out var fr) ? " · " + fr.GetString() : string.Empty;
        if (upload is "failed" or "rejected" or "deleted" || processing is "failed" or "terminated")
        {
            return new ProcessingCheck(TargetStatus.Failed, Message: $"YouTube: uploadStatus={upload}, processingStatus={processing}{reason}");
        }

        return upload == "processed" || processing == "succeeded" ? new ProcessingCheck(TargetStatus.Published) : new ProcessingCheck(TargetStatus.Processing);
    }

    /// <summary>videos.insert body; limits apply here too (YouTube rejects an over-long request entirely).</summary>
    internal static string MetadataJson(PublishRequest request)
    {
        var target = request.Target;
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WritePropertyName("snippet");
            w.WriteStartObject();
            w.WriteString("title", target.Title);
            w.WriteString("description", target.Text);
            w.WritePropertyName("tags");
            w.WriteStartArray();
            foreach (var tag in target.Keywords)
            {
                w.WriteStringValue(tag);
            }

            w.WriteEndArray();
            w.WriteString("categoryId", target.Options.GetValueOrDefault(TargetOptionKeys.YouTubeCategory) is { Length: > 0 } category ? category : "22");
            w.WriteEndObject();
            w.WritePropertyName("status");
            w.WriteStartObject();
            var privacy = target.Options.GetValueOrDefault(TargetOptionKeys.Privacy) is ("public" or "unlisted" or "private") and var chosen ? chosen : "private";
            w.WriteString("privacyStatus", privacy);
            w.WriteBoolean("selfDeclaredMadeForKids", Flag(target, TargetOptionKeys.MadeForKids));
            w.WriteBoolean("containsSyntheticMedia", Flag(target, TargetOptionKeys.Synthetic));
            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var (reason, message) = ParseError(body);
        return status == 401 ? new PlatformAuthException($"YouTube rejected the token ({step}, HTTP 401).")
            : status == 403 && IsQuota(reason) ? new PlatformQuotaException(QuotaMessage(reason, message))
            : new PlatformRejectedException($"YouTube {step} failed (HTTP {status}{With(reason)}): {message}");
    }

    /// <summary>Google error body: <c>error.errors[0].reason</c> and <c>error.message</c>.</summary>
    internal static (string Reason, string Message) ParseError(string body)
    {
        var root = JsonSafe.TryParse(body);
        if (root?["error"] is System.Text.Json.Nodes.JsonObject error)
        {
            var reason = JsonSafe.Str(error["errors"]?[0], "reason");
            if (reason.Length == 0)
            {
                reason = JsonSafe.Str(error, "status");
            }

            var message = JsonSafe.Str(error, "message");
            return (reason, message.Length > 0 ? message : PlatformText.Short(body));
        }

        return (string.Empty, PlatformText.Short(body));
    }

    private async Task<string> OpenSessionAsync(PublishRequest request, TokenSession auth, long size, string type, CancellationToken cancellationToken)
    {
        var metadata = MetadataJson(request);
        var failures = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var message = new HttpRequestMessage(HttpMethod.Post, UploadApi + "videos?uploadType=resumable&part=snippet,status");
            Authorize(message, auth.Token);
            message.Headers.TryAddWithoutValidation("X-Upload-Content-Length", size.ToString(CultureInfo.InvariantCulture));
            message.Headers.TryAddWithoutValidation("X-Upload-Content-Type", type);
            message.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await Http.Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNetwork(ex, cancellationToken))
            {
                if (++failures >= MaxAttempts)
                {
                    throw new PlatformRejectedException($"YouTube could not be reached after {MaxAttempts} attempts: {ex.Message}", ex);
                }

                await BackOffAsync(failures, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var location = response.Headers.Location?.ToString() ?? string.Empty;
                    return location.Length > 0 ? location : throw new PlatformRejectedException("YouTube opened the upload session but returned no Location header.");
                }

                var code = (int)response.StatusCode;
                var (reason, text) = ParseError(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (code == 403 && IsQuota(reason))
                {
                    throw new PlatformQuotaException(QuotaMessage(reason, text));
                }

                if (code == 401)
                {
                    await auth.RenewOrFailAsync($"YouTube rejected the token (HTTP 401): {text}", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (IsTransient(code, reason) && ++failures < MaxAttempts)
                {
                    await BackOffAsync(failures, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new PlatformRejectedException($"YouTube did not start the upload (HTTP {code}{With(reason)}): {text}");
            }
        }
    }

    private async Task<string> SendChunksAsync(string sessionUrl, FileInfo file, string type, TokenSession auth, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var size = file.Length;
        await using var stream = file.OpenRead();
        var buffer = new byte[ChunkBytes];
        long offset = 0;
        var failures = 0;

        async Task<string?> SyncAsync()
        {
            try
            {
                var (o, id) = await QueryOffsetAsync(sessionUrl, size, auth, cancellationToken).ConfigureAwait(false);
                if (id is not null)
                {
                    progress?.Report(1);
                    return id;
                }

                offset = Math.Clamp(o, 0, size);
                progress?.Report(offset / (double)size);
            }
            catch (Exception ex) when (ex is not (UploadSessionLostException or PlatformQuotaException or OperationCanceledException or PlatformAuthException))
            {
                // The resume point could not be asked: continue from the last known position (the server rejects anything wrong).
                Logger.LogWarning("YouTube resume point could not be queried: {Message}", ex.Message);
            }

            return null;
        }

        while (offset < size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = offset;
            var n = await stream.ReadAtLeastAsync(buffer.AsMemory(0, (int)Math.Min(ChunkBytes, size - offset)), (int)Math.Min(ChunkBytes, size - offset),
                throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            if (n <= 0)
            {
                throw new PlatformRejectedException($"The video could not be read after byte {offset}; the file may have changed.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Put, sessionUrl);
            Authorize(message, auth.Token);
            message.Content = new ProgressContent(buffer, n, offset, size, progress);
            message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
            message.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + n - 1, size);
            HttpResponseMessage response;
            try
            {
                response = await _chunks.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNetwork(ex, cancellationToken))
            {
                if (++failures >= MaxAttempts)
                {
                    throw new PlatformRejectedException($"YouTube upload broke at {offset / 1048576.0:0.#} MB and did not recover in {MaxAttempts} attempts: {ex.Message}", ex);
                }

                Logger.LogWarning("YouTube chunk failed at {Megabytes:0.#} MB ({Message}), attempt {Attempt}", offset / 1048576.0, ex.Message, failures);
                await BackOffAsync(failures, cancellationToken).ConfigureAwait(false);
                if (await SyncAsync().ConfigureAwait(false) is { } done)
                {
                    return done;
                }

                continue;
            }

            using (response)
            {
                var code = (int)response.StatusCode;
                if (code == 308)
                {
                    failures = 0;
                    offset = Math.Clamp(RangeEnd(response) is { } end ? end + 1 : offset + n, 0, size);
                    progress?.Report(offset / (double)size);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    progress?.Report(1);
                    return VideoId(body);
                }

                var (reason, text) = ParseError(body);
                if (code is 404 or 410)
                {
                    throw new UploadSessionLostException($"HTTP {code}{With(reason)}");
                }

                if (code == 403 && IsQuota(reason))
                {
                    throw new PlatformQuotaException(QuotaMessage(reason, text));
                }

                if (code == 401)
                {
                    await auth.RenewOrFailAsync($"YouTube rejected the token during upload (HTTP 401): {text}", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (IsTransient(code, reason) && ++failures < MaxAttempts)
                {
                    await BackOffAsync(failures, cancellationToken).ConfigureAwait(false);
                    if (await SyncAsync().ConfigureAwait(false) is { } done)
                    {
                        return done;
                    }

                    continue;
                }

                throw new PlatformRejectedException($"YouTube rejected the upload (HTTP {code}{With(reason)}, at {offset / 1048576.0:0.#} MB): {text}");
            }
        }

        // Every byte went but no 200 was seen: not silently "done"; the server is asked.
        return await SyncAsync().ConfigureAwait(false)
            ?? throw new PlatformRejectedException("YouTube received all bytes but did not confirm; the video id is unknown. Check the channel.");
    }

    private async Task<(long Offset, string? VideoId)> QueryOffsetAsync(string sessionUrl, long size, TokenSession auth, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, sessionUrl);
        Authorize(message, auth.Token);
        message.Content = new ByteArrayContent([]);
        message.Content.Headers.ContentRange = new ContentRangeHeaderValue(size);
        using var response = await Http.Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var code = (int)response.StatusCode;
        if (code == 308)
        {
            return (RangeEnd(response) is { } end ? end + 1 : 0, null);
        }

        if (response.IsSuccessStatusCode)
        {
            return (size, VideoId(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
        }

        if (code is 404 or 410)
        {
            throw new UploadSessionLostException($"status query HTTP {code}");
        }

        var (reason, text) = ParseError(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        throw code == 403 && IsQuota(reason)
            ? new PlatformQuotaException(QuotaMessage(reason, text))
            : new PlatformRejectedException($"YouTube resume point could not be queried (HTTP {code}{With(reason)}): {text}");
    }

    private static bool IsTransient(int code, string reason) =>
        code >= 500 || code == 429 || (code == 403 && reason is "rateLimitExceeded" or "userRateLimitExceeded" or "backendError");

    private static bool IsQuota(string reason) => reason is "quotaExceeded" or "dailyLimitExceeded" or "uploadLimitExceeded";

    private static string QuotaMessage(string reason, string message) =>
        $"YouTube daily upload quota is used up ({(reason.Length > 0 ? reason : "quotaExceeded")}); it resets at midnight Pacific Time."
        + (message.Length > 0 ? " Google: " + message : string.Empty);

    private static string VideoId(string body) =>
        JsonSafe.Str(JsonSafe.TryParse(body), "id") is { Length: > 0 } id
            ? id
            : throw new PlatformRejectedException("YouTube finished the upload but returned no video id; the video MAY be on the channel. Check it.");

    private static long? RangeEnd(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Range", out var values))
        {
            return null;
        }

        var s = values.FirstOrDefault() ?? string.Empty;
        var i = s.LastIndexOf('-');
        return i >= 0 && i + 1 < s.Length && long.TryParse(s[(i + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var end) ? end : null;
    }

    private static bool IsNetwork(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && ex is HttpRequestException or IOException or TaskCanceledException;

    private Task BackOffAsync(int attempt, CancellationToken cancellationToken) =>
        Delay(TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Clamp(attempt, 1, 6)))), cancellationToken);

    private static string With(string reason) => reason.Length > 0 ? " " + reason : string.Empty;
}
