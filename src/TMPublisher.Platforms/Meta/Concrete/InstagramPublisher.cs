using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>
/// Instagram Reels with Facebook Login (graph.facebook.com). Meta documents the local-file "resumable" upload only for apps that use
/// Facebook Login for Business; TM AutoClip's live uploader used it on the Instagram Login path, which the documentation does not cover.
/// Here it is used on the documented path:
/// <list type="number">
/// <item>Quota: <c>GET /{ig-user}/content_publishing_limit</c> (100 posts in 24 hours) before anything is uploaded.</item>
/// <item>Container: <c>POST /{ig-user}/media</c> media_type=REELS, upload_type=resumable, caption, share_to_feed → id + upload address.</item>
/// <item>Bytes: <c>POST {upload address}</c> with <c>Authorization: OAuth {token}</c> (not Bearer), <c>offset</c>, <c>file_size</c>.</item>
/// <item>Processing: <c>status_code</c> must be FINISHED before publishing (ERROR → the reason).</item>
/// <item>Publish: <c>POST /{ig-user}/media_publish</c>; the address is the media's <c>permalink</c> (empty if unreadable: publishing again
/// would post the Reel twice).</item>
/// </list>
/// There is no private / unlisted Reel: it is visible like the account.
/// </summary>
public sealed class InstagramPublisher(HttpTransport transport, ILogger<InstagramPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    private static readonly TimeSpan ProcessingLimit = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = CheckFile(request.Post.Media.Single(), 300L * 1024 * 1024, "Instagram");
        var user = request.Credentials.Require(MetaKeys.InstagramId);
        var token = request.Credentials.Require(CredentialKeys.AccessToken);
        await CheckQuotaAsync(user, token, cancellationToken).ConfigureAwait(false);

        var container = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, MetaGraph.Root + user + "/media")
        {
            Content = new FormUrlEncodedContent(
            [
                new("media_type", "REELS"), new("upload_type", "resumable"), new("caption", request.Target.Text), new("share_to_feed", "true"),
                new("access_token", token),
            ]),
        }, null, "container", cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(container, "id");
        var upload = JsonSafe.Str(container, "uri");
        if (id.Length == 0 || upload.Length == 0)
        {
            throw new PlatformRejectedException("Instagram returned no container or upload address; the token may lack instagram_content_publish.");
        }

        progress?.Report(0.1);
        await SendJsonAsync(() =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, upload) { Content = new StreamContent(file.OpenRead()) };
            message.Headers.TryAddWithoutValidation("Authorization", "OAuth " + token);
            message.Headers.TryAddWithoutValidation("offset", "0");
            message.Headers.TryAddWithoutValidation("file_size", file.Length.ToString(CultureInfo.InvariantCulture));
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            message.Content.Headers.ContentLength = file.Length;
            return message;
        }, null, "upload", cancellationToken).ConfigureAwait(false);
        progress?.Report(0.6);

        var round = 0;
        await PollAsync(async (_, ct) =>
        {
            var state = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, MetaGraph.Root + id + "?fields=status_code,status&access_token=" + Uri.EscapeDataString(token)),
                null, "processing", ct).ConfigureAwait(false);
            progress?.Report(Math.Min(0.85, 0.6 + (++round * 0.01)));
            return JsonSafe.Str(state, "status_code") switch
            {
                "FINISHED" => PollStep<bool>.Done(true),
                "ERROR" or "EXPIRED" => throw new PlatformRejectedException("Instagram could not process the video: " + JsonSafe.Str(state, "status")),
                _ => PollStep<bool>.Again(TimeSpan.FromSeconds(5)),
            };
        }, TimeSpan.Zero, ProcessingLimit, () => new PlatformRejectedException("Instagram did not process the video in 15 minutes."), cancellationToken).ConfigureAwait(false);

        var published = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, MetaGraph.Root + user + "/media_publish")
        {
            Content = new FormUrlEncodedContent([new("creation_id", id), new("access_token", token)]),
        }, null, "publish", cancellationToken).ConfigureAwait(false);
        var media = JsonSafe.Str(published, "id");
        var permalink = await ReadAfterPublishAsync("Instagram permalink", media, async ct => JsonSafe.Str(await SendJsonAsync(() => new HttpRequestMessage(
            HttpMethod.Get, MetaGraph.Root + media + "?fields=permalink&access_token=" + Uri.EscapeDataString(token)), null, "permalink", ct).ConfigureAwait(false),
            "permalink"), cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
        Logger.LogInformation("Instagram Reel {MediaId} published", media);
        return new PublishResult(media, permalink, TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body) => MetaErrors.Classify("Instagram", step, status, body);

    private async Task CheckQuotaAsync(string user, string token, CancellationToken cancellationToken)
    {
        var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, MetaGraph.Root + user
            + "/content_publishing_limit?fields=quota_usage,config&access_token=" + Uri.EscapeDataString(token)), null, null, cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess)
        {
            // The limit endpoint is informational; a failure here is logged and the upload goes on (the publish answer would carry the limit).
            Logger.LogWarning("Instagram publishing limit could not be read (HTTP {Status})", reply.Status);
            return;
        }

        var entry = (JsonSafe.TryParse(reply.Body)?["data"] as JsonArray)?.FirstOrDefault();
        if (int.TryParse(JsonSafe.Str(entry, "quota_usage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var used)
            && int.TryParse(JsonSafe.Str(entry?["config"], "quota_total"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) && used >= total)
        {
            throw new PlatformQuotaException($"Instagram's limit of {total} posts in 24 hours is reached for this account.");
        }
    }
}
