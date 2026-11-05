using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>
/// Facebook Page with the Page token: text → <c>/{page}/feed</c>; one photo → <c>/{page}/photos</c> (file from this computer); several
/// photos → each uploaded unpublished, then one feed post with <c>attached_media</c>; video → <c>graph-video …/{page}/videos</c> (file from
/// this computer, description = text). Address: <c>facebook.com/{post id}</c>.
/// </summary>
public sealed class FacebookPublisher(HttpTransport transport, ILogger<FacebookPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = request.Credentials.Require(CredentialKeys.PageId);
        var token = request.Credentials.Require(CredentialKeys.AccessToken);
        var post = request.Post;
        var text = request.Target.Text;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Photo ? 10L * 1024 * 1024 : 1024L * 1024 * 1024, "Facebook");
        }

        progress?.Report(0.05);
        JsonNode? answer;
        if (post.Media.Count == 0)
        {
            answer = await FormAsync(MetaGraph.Root + page + "/feed", [new("message", text), new("access_token", token)], "post", cancellationToken).ConfigureAwait(false);
        }
        else if (post.Kind == PostKind.Video)
        {
            var path = post.Media[0].Path;
            answer = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, MetaGraph.VideoRoot + page + "/videos")
            {
                Content = new MultipartFormDataContent
                {
                    { new StringContent(token), "access_token" }, { new StringContent(text), "description" }, { FilePart(path), "source", Path.GetFileName(path) },
                },
            }, null, "video upload", cancellationToken).ConfigureAwait(false);
            var id = JsonSafe.Str(answer, "id");
            progress?.Report(1);
            Logger.LogInformation("Facebook video {VideoId} uploaded", id);
            return new PublishResult(id, "https://www.facebook.com/" + Uri.EscapeDataString(page) + "/videos/" + Uri.EscapeDataString(id), TargetStatus.Processing);
        }
        else if (post.Media.Count == 1)
        {
            answer = await PhotoAsync(page, token, post.Media[0].Path, text, published: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var form = new List<KeyValuePair<string, string>> { new("message", text), new("access_token", token) };
            for (var i = 0; i < post.Media.Count; i++)
            {
                var photo = await PhotoAsync(page, token, post.Media[i].Path, string.Empty, published: false, cancellationToken).ConfigureAwait(false);
                form.Add(new("attached_media[" + i.ToString(CultureInfo.InvariantCulture) + "]", new JsonObject { ["media_fbid"] = JsonSafe.Str(photo, "id") }.ToJsonString()));
                progress?.Report(0.1 + (0.7 * (i + 1) / post.Media.Count));
            }

            answer = await FormAsync(MetaGraph.Root + page + "/feed", form, "multi-photo post", cancellationToken).ConfigureAwait(false);
        }

        var postId = JsonSafe.Str(answer, "post_id") is { Length: > 0 } p ? p : JsonSafe.Str(answer, "id");
        progress?.Report(1);
        Logger.LogInformation("Facebook post {PostId} created", postId);
        return new PublishResult(postId, "https://www.facebook.com/" + Uri.EscapeDataString(postId), TargetStatus.Published);
    }

    /// <inheritdoc />
    public override async Task<ProcessingCheck> CheckProcessingAsync(PublishRequest request, string externalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var answer = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, MetaGraph.Root + Uri.EscapeDataString(externalId)
            + "?fields=status&access_token=" + Uri.EscapeDataString(request.Credentials.Require(CredentialKeys.AccessToken))), null, "video status", cancellationToken).ConfigureAwait(false);
        var state = JsonSafe.Str(answer?["status"], "video_status");
        return state switch
        {
            "ready" => new ProcessingCheck(TargetStatus.Published),
            "error" => new ProcessingCheck(TargetStatus.Failed, Message: "Facebook could not process the video."),
            _ => new ProcessingCheck(TargetStatus.Processing),
        };
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body) => MetaErrors.Classify("Facebook", step, status, body);

    private Task<JsonNode?> PhotoAsync(string page, string token, string path, string caption, bool published, CancellationToken cancellationToken) =>
        SendJsonAsync(() =>
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(token), "access_token" },
                { new StringContent(published ? "true" : "false"), "published" },
                { FilePart(path), "source", Path.GetFileName(path) },
            };
            if (caption.Length > 0)
            {
                form.Add(new StringContent(caption), "message");
            }

            return new HttpRequestMessage(HttpMethod.Post, MetaGraph.Root + page + "/photos") { Content = form };
        }, null, "photo upload", cancellationToken);

    private Task<JsonNode?> FormAsync(string address, IEnumerable<KeyValuePair<string, string>> form, string step, CancellationToken cancellationToken) =>
        SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, address) { Content = new FormUrlEncodedContent(form) }, null, step, cancellationToken);
}
