using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Pinterest;

/// <summary>
/// Pinterest v5: image pin → <c>POST /pins</c> with <c>image_base64</c>; video pin → <c>POST /media</c> (upload address and form fields),
/// multipart upload of the file to that address, wait until the media is <c>succeeded</c>, then <c>POST /pins</c> with <c>video_id</c>.
/// Board from the target's option or the account's default board.
/// </summary>
public sealed class PinterestPublisher(HttpTransport transport, ILogger<PinterestPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(30), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var media = request.Post.Media.Single();
        var file = CheckFile(media, media.Type == MediaType.Photo ? 20L * 1024 * 1024 : 2L * 1024 * 1024 * 1024, "Pinterest");
        var session = Session(request);
        var board = request.Target.Options.GetValueOrDefault(TargetOptionKeys.PinterestBoard) is { Length: > 0 } chosen ? chosen : request.Credentials.Require(PinterestApi.BoardKey);
        var pin = new JsonObject { ["board_id"] = board, ["title"] = request.Target.Title, ["description"] = request.Target.Text };
        if (request.Target.Options.GetValueOrDefault(TargetOptionKeys.PinterestLink) is { Length: > 0 } link)
        {
            pin["link"] = link;
        }

        if (media.Type == MediaType.Photo)
        {
            pin["media_source"] = new JsonObject
            {
                ["source_type"] = "image_base64",
                ["content_type"] = MediaTypes.Of(file.FullName),
                ["data"] = Convert.ToBase64String(await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false)),
            };
        }
        else
        {
            var mediaId = await UploadVideoAsync(file, session, progress, cancellationToken).ConfigureAwait(false);
            pin["media_source"] = new JsonObject { ["source_type"] = "video_id", ["media_id"] = mediaId, ["cover_image_key_frame_time"] = 0 };
        }

        var created = await SendJsonAsync(() => Json(PinterestApi.Root + "pins", pin), session, "pin", cancellationToken).ConfigureAwait(false);
        var id = JsonSafe.Str(created, "id");
        progress?.Report(1);
        Logger.LogInformation("Pinterest pin {PinId} created", id);
        return new PublishResult(id, "https://www.pinterest.com/pin/" + Uri.EscapeDataString(id) + "/", TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var text = $"Pinterest {step} failed (HTTP {status}): {(JsonSafe.Str(JsonSafe.TryParse(body), "message") is { Length: > 0 } m ? m : PlatformText.Short(body))}";
        return status switch
        {
            401 => new PlatformAuthException(text),
            403 => new PlatformRejectedException(text + " With Trial access, or on a personal (non-business) account, Pinterest may refuse; see the setup guide."),
            429 => new PlatformQuotaException(text),
            _ => new PlatformRejectedException(text),
        };
    }

    private async Task<string> UploadVideoAsync(FileInfo file, TokenSession session, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var registered = await SendJsonAsync(() => Json(PinterestApi.Root + "media", new JsonObject { ["media_type"] = "video" }), session, "media register", cancellationToken)
            .ConfigureAwait(false);
        var mediaId = JsonSafe.Str(registered, "media_id");
        var uploadUrl = JsonSafe.Str(registered, "upload_url");
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in registered?["upload_parameters"] as JsonObject ?? [])
        {
            form.Add(new StringContent(value?.ToString() ?? string.Empty), key);
        }

        form.Add(FilePart(file.FullName), "file", file.Name);
        var upload = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = form }, cancellationToken).ConfigureAwait(false);
        if (!upload.IsSuccess)
        {
            throw Classify("video upload", upload.Status, upload.Body);
        }

        progress?.Report(0.7);
        await PollAsync(async (_, ct) =>
        {
            var state = JsonSafe.Str(await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, PinterestApi.Root + "media/" + Uri.EscapeDataString(mediaId)),
                session, "media status", ct).ConfigureAwait(false), "status");
            return state switch
            {
                "succeeded" => PollStep<bool>.Done(true),
                "failed" => throw new PlatformRejectedException("Pinterest could not process the video."),
                _ => PollStep<bool>.Again(TimeSpan.FromSeconds(5)),
            };
        }, TimeSpan.Zero, TimeSpan.FromMinutes(15), () => new PlatformRejectedException("Pinterest did not process the video in 15 minutes."), cancellationToken).ConfigureAwait(false);
        return mediaId;
    }

    private static HttpRequestMessage Json(string address, JsonObject body) =>
        new(HttpMethod.Post, address) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
