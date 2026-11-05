using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Mastodon;

/// <summary>
/// Mastodon: media → <c>POST /api/v2/media</c> (202 = still processing: <c>GET /api/v1/media/{id}</c> until it has an address), then
/// <c>POST /api/v1/statuses</c> with the text, media ids and visibility (public / unlisted / private).
/// </summary>
public sealed class MastodonPublisher(HttpTransport transport, ILogger<MastodonPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(15), logger)
{
    private static readonly TimeSpan MediaLimit = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var server = request.Credentials.Require(CredentialKeys.Server);
        var session = Session(request);
        var post = request.Post;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Photo ? 16L * 1024 * 1024 : 99L * 1024 * 1024, "Mastodon");
        }

        var ids = new List<string>();
        for (var i = 0; i < post.Media.Count; i++)
        {
            var path = post.Media[i].Path;
            var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, server + "/api/v2/media")
            {
                Content = new MultipartFormDataContent { { FilePart(path), "file", Path.GetFileName(path) } },
            }, session, null, cancellationToken).ConfigureAwait(false);
            if (!reply.IsSuccess)
            {
                throw Classify("media upload", reply.Status, reply.Body);
            }

            var id = JsonSafe.Str(JsonSafe.TryParse(reply.Body), "id");
            if (reply.Status == 202)
            {
                await WaitMediaAsync(server, id, session, cancellationToken).ConfigureAwait(false);
            }

            ids.Add(id);
            progress?.Report(0.1 + (0.7 * (i + 1) / post.Media.Count));
        }

        var visibility = request.Target.Options.GetValueOrDefault(TargetOptionKeys.Privacy) switch
        {
            "unlisted" => "unlisted",
            "private" => "private",
            _ => "public",
        };
        var body = new JsonObject { ["status"] = request.Target.Text, ["visibility"] = visibility, ["media_ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) };
        var status = await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, server + "/api/v1/statuses")
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Idempotency-Key", request.Target.Id.ToString("N") } },
        }, session, "post", cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
        Logger.LogInformation("Mastodon status {StatusId} posted", JsonSafe.Str(status, "id"));
        return new PublishResult(JsonSafe.Str(status, "id"), JsonSafe.Str(status, "url"), TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var text = $"Mastodon {step} failed (HTTP {status}): {(JsonSafe.Str(JsonSafe.TryParse(body), "error") is { Length: > 0 } e ? e : PlatformText.Short(body))}";
        return status switch
        {
            401 or 403 => new PlatformAuthException(text),
            429 => new PlatformQuotaException(text),
            _ => new PlatformRejectedException(text),
        };
    }

    private Task<bool> WaitMediaAsync(string server, string id, TokenSession session, CancellationToken cancellationToken) =>
        PollAsync(async (_, ct) =>
        {
            var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, server + "/api/v1/media/" + Uri.EscapeDataString(id)), session, null, ct).ConfigureAwait(false);
            return reply.Status == 200 && JsonSafe.Str(JsonSafe.TryParse(reply.Body), "url").Length > 0
                ? PollStep<bool>.Done(true)
                : reply.Status is 200 or 206
                    ? PollStep<bool>.Again(TimeSpan.FromSeconds(3))
                    : throw Classify("media processing", reply.Status, reply.Body);
        }, TimeSpan.FromSeconds(2), MediaLimit, () => new PlatformRejectedException("Mastodon did not finish processing the media in 10 minutes."), cancellationToken);
}
