using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.TikTok;

/// <summary>
/// TikTok Content Posting API (ported from TM AutoClip; the inbox path added):
/// <list type="bullet">
/// <item>Draft (default): <c>POST /v2/post/publish/inbox/video/init/</c> (source only), chunks, status until <c>SEND_TO_USER_INBOX</c> →
/// "waiting for approval": the user finishes the post in the TikTok app (the caption is written there; drafts carry no text).</item>
/// <item>Direct (optional): creator info first (allowed privacy levels; an unaudited app gets only <c>SELF_ONLY</c>), then
/// <c>POST /v2/post/publish/video/init/</c> with the caption and <c>SELF_ONLY</c>, chunks, status until <c>PUBLISH_COMPLETE</c>.</item>
/// <item>Chunks: <c>PUT</c> to the signed upload address with <c>Content-Range</c> and NO authorization header (it breaks the signature);
/// 5-64 MB, at most 1000, the last carries the rest; network errors and 5xx resend the same range.</item>
/// </list>
/// Rate: 6 requests per minute per token (sliding window).
/// </summary>
public sealed class TikTokPublisher(HttpTransport transport, ILogger<TikTokPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    /// <summary>Smallest chunk (except the last).</summary>
    public const long MinChunk = 5L * 1024 * 1024;

    /// <summary>Largest chunk.</summary>
    public const long MaxChunk = 64L * 1024 * 1024;

    /// <summary>Most chunks.</summary>
    public const int MaxChunks = 1000;

    private const int RequestsPerMinute = 6;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(12);
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly SemaphoreSlim _rate = new(1, 1);

    /// <summary>Preferred chunk (smaller in tests).</summary>
    internal long ChunkBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>Lower chunk bound (smaller in tests).</summary>
    internal long MinChunkBytes { get; set; } = MinChunk;

    /// <summary>Chunk size and count: one chunk when the file is smaller than the preferred size; otherwise a size that fits 1000 chunks.</summary>
    public static (long Chunk, int Count) ChunkPlan(long size, long preferred, long minimum = MinChunk)
    {
        if (size <= preferred)
        {
            return (size, 1);
        }

        var chunk = preferred;
        if (size / chunk > MaxChunks)
        {
            chunk = Math.Min(MaxChunk, (size + MaxChunks - 1) / MaxChunks);
        }

        chunk = Math.Clamp(chunk, minimum, MaxChunk);
        return (chunk, (int)Math.Max(1, size / chunk));
    }

    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = CheckFile(request.Post.Media.Single(), 4L * 1024 * 1024 * 1024, "TikTok");
        var session = Session(request);
        var direct = request.Target.Options.GetValueOrDefault(TargetOptionKeys.TikTokMode) == "direct";
        var (chunk, count) = ChunkPlan(file.Length, ChunkBytes, MinChunkBytes);
        var source = new JsonObject { ["source"] = "FILE_UPLOAD", ["video_size"] = file.Length, ["chunk_size"] = chunk, ["total_chunk_count"] = count };
        JsonNode? data;
        if (direct)
        {
            await CheckCreatorAsync(session, cancellationToken).ConfigureAwait(false);
            var init = new JsonObject
            {
                ["post_info"] = new JsonObject
                {
                    ["title"] = request.Target.Text,
                    ["privacy_level"] = "SELF_ONLY",
                    ["disable_comment"] = false,
                    ["disable_duet"] = false,
                    ["disable_stitch"] = false,
                    ["is_aigc"] = Flag(request.Target, TargetOptionKeys.Synthetic),
                },
                ["source_info"] = source,
            };
            data = await CallAsync("post/publish/video/init/", init.ToJsonString(), session, "init", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            data = await CallAsync("post/publish/inbox/video/init/", new JsonObject { ["source_info"] = source }.ToJsonString(), session, "inbox init", cancellationToken)
                .ConfigureAwait(false);
        }

        var publishId = JsonSafe.Str(data, "publish_id");
        var uploadUrl = JsonSafe.Str(data, "upload_url");
        if (publishId.Length == 0 || uploadUrl.Length == 0)
        {
            throw new PlatformRejectedException("TikTok returned no publish_id or upload_url.");
        }

        progress?.Report(0.05);
        await SendChunksAsync(file, uploadUrl, chunk, count, progress, cancellationToken).ConfigureAwait(false);
        var (state, postId) = await WaitAsync(publishId, session, progress, cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
        Logger.LogInformation("TikTok {Mode} upload finished ({State})", direct ? "direct" : "draft", state);
        return state == "SEND_TO_USER_INBOX"
            ? new PublishResult(publishId, string.Empty, TargetStatus.NeedsApproval, "note.tiktok_approve")
            : new PublishResult(postId.Length > 0 ? postId : publishId, string.Empty, TargetStatus.Published, "note.tiktok_private");
    }

    /// <inheritdoc />
    protected override bool IsTokenRejected(PlatformReply reply) =>
        base.IsTokenRejected(reply) || JsonSafe.Str(JsonSafe.TryParse(reply.Body)?["error"], "code") == "access_token_invalid";

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var error = JsonSafe.TryParse(body)?["error"];
        var code = JsonSafe.Str(error, "code");
        var text = $"TikTok {step} failed (HTTP {status}, {(code.Length > 0 ? code : "no code")}): {JsonSafe.Str(error, "message")}";
        return code switch
        {
            "access_token_invalid" or "scope_not_authorized" or "scope_permission_missed" => new PlatformAuthException(text),
            "spam_risk_too_many_posts" or "spam_risk_too_many_pending_share" or "reached_active_user_cap" => new PlatformQuotaException(text),
            "unaudited_client_can_only_post_to_private_accounts" => new PlatformRejectedException(text + " TikTok lets an app it has not audited post directly only to a private TikTok account: set the TikTok account itself to private (TikTok app → Settings → Privacy), or use the draft mode."),
            _ when status == 401 => new PlatformAuthException(text),
            _ => new PlatformRejectedException(text),
        };
    }

    private async Task CheckCreatorAsync(TokenSession session, CancellationToken cancellationToken)
    {
        var data = await CallAsync("post/publish/creator_info/query/", "{}", session, "creator info", cancellationToken).ConfigureAwait(false);
        var options = (data?["privacy_level_options"] as JsonArray ?? []).Select(o => o?.ToString() ?? string.Empty).ToList();
        if (options.Count > 0 && !options.Contains("SELF_ONLY"))
        {
            throw new PlatformRejectedException("TikTok does not allow a private post for this account (allowed: " + string.Join(", ", options) + ").");
        }
    }

    private async Task SendChunksAsync(FileInfo file, string uploadUrl, long chunk, int count, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var mime = MediaTypes.Of(file.FullName);
        await using var stream = file.OpenRead();
        long position = 0;
        for (var i = 0; i < count; i++)
        {
            var length = i == count - 1 ? file.Length - position : chunk;
            var buffer = new byte[length];
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            await PutAsync(uploadUrl, buffer, position, file.Length, mime, i + 1, count, cancellationToken).ConfigureAwait(false);
            position += length;
            progress?.Report(0.05 + (0.85 * position / file.Length));
        }
    }

    private async Task PutAsync(string uploadUrl, byte[] buffer, long first, long total, string mime, int index, int count, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = new ByteArrayContent(buffer) };
                message.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
                message.Content.Headers.ContentRange = new ContentRangeHeaderValue(first, first + buffer.Length - 1, total);
                var reply = await Http.SendAsync(message, cancellationToken).ConfigureAwait(false);
                if (reply.IsSuccess)
                {
                    return;
                }

                if ((reply.Status < 500 && reply.Status != (int)HttpStatusCode.RequestTimeout) || attempt >= 3)
                {
                    throw new PlatformRejectedException($"TikTok rejected chunk {index}/{count} (HTTP {reply.Status}): {PlatformText.Short(reply.Body)}");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested && attempt < 3)
            {
                Logger.LogWarning("TikTok chunk {Index}/{Count} interrupted ({Message}); sending it again", index, count, ex.Message);
            }

            await Delay(TimeSpan.FromSeconds(3 * attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(string State, string PostId)> WaitAsync(string publishId, TokenSession session, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["publish_id"] = publishId }.ToJsonString();
        var last = string.Empty;
        return await PollAsync(async (round, ct) =>
        {
            var data = await CallAsync("post/publish/status/fetch/", body, session, "status", ct).ConfigureAwait(false);
            last = JsonSafe.Str(data, "status");
            switch (last)
            {
                case "SEND_TO_USER_INBOX":
                    return PollStep<(string, string)>.Done((last, string.Empty));
                case "PUBLISH_COMPLETE":
                    return PollStep<(string, string)>.Done((last, (data?["publicaly_available_post_id"] as JsonArray)?.FirstOrDefault()?.ToString() ?? string.Empty));
                case "FAILED":
                    var reason = JsonSafe.Str(data, "fail_reason");
                    throw reason.StartsWith("spam_risk", StringComparison.Ordinal)
                        ? new PlatformQuotaException("TikTok: too many posts or drafts waiting (" + reason + ").")
                        : reason == "auth_removed"
                            ? new PlatformAuthException("TikTok: the account removed the app's access (auth_removed).")
                            : new PlatformRejectedException("TikTok could not take the video: " + (reason.Length > 0 ? reason : "no reason given"));
                default:
                    progress?.Report(Math.Min(0.97, 0.9 + (round * 0.002)));
                    return PollStep<(string, string)>.Again(PollInterval);
            }
        }, PollInterval, PollInterval * 100,
            () => new PlatformRejectedException($"TikTok did not finish in time (last status: {(last.Length > 0 ? last : "unknown")}). Check the TikTok app before sending again."),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonNode?> CallAsync(string path, string json, TokenSession session, string step, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, TikTokApi.Root + path) { Content = new StringContent(json, Encoding.UTF8, "application/json") },
                session, ThrottleAsync, cancellationToken).ConfigureAwait(false);
            var root = JsonSafe.TryParse(reply.Body);
            var code = JsonSafe.Str(root?["error"], "code");
            if ((reply.Status == 429 || code == "rate_limit_exceeded") && attempt < 4)
            {
                await Delay(reply.RetryAfter ?? TimeSpan.FromSeconds(15 * attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (reply.IsSuccess && code is "ok" or "" && root?["data"] is { } data)
            {
                return data;
            }

            throw Classify(step, reply.Status, reply.Body);
        }
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        await _rate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            while (_calls.Count > 0 && now - _calls.Peek() >= TimeSpan.FromMinutes(1))
            {
                _calls.Dequeue();
            }

            if (_calls.Count >= RequestsPerMinute)
            {
                await Delay(_calls.Peek() + TimeSpan.FromMinutes(1) - now, cancellationToken).ConfigureAwait(false);
                _calls.Dequeue();
            }

            _calls.Enqueue(DateTimeOffset.UtcNow);
        }
        finally
        {
            _rate.Release();
        }
    }
}
