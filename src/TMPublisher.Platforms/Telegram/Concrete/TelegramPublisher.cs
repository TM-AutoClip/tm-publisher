using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Telegram;

/// <summary>
/// Telegram Bot API: text → sendMessage; one photo → sendPhoto; one video → sendVideo; several photos or a mix → sendMediaGroup (2-10,
/// caption on the first item). Files are uploaded from this computer (multipart). The address of a public channel's message is
/// <c>t.me/{username}/{message_id}</c>; private chats have no public address.
/// </summary>
public sealed class TelegramPublisher(HttpTransport transport, ILogger<TelegramPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(20), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var api = TelegramApi.Base(request.Credentials.Require(CredentialKeys.BotToken));
        var chat = request.Credentials.Require(CredentialKeys.ChatId);
        var post = request.Post;
        var text = request.Target.Text;
        foreach (var media in post.Media)
        {
            CheckFile(media, media.Type == MediaType.Photo ? 10L * 1024 * 1024 : TelegramPlatform.MaxFileBytes, "Telegram");
        }

        progress?.Report(0.05);
        JsonNode? result;
        string method;
        if (post.Media.Count == 0)
        {
            method = "sendMessage";
            result = await CallAsync(api, method, () => JsonContent.Create(new JsonObject { ["chat_id"] = chat, ["text"] = text }), cancellationToken).ConfigureAwait(false);
        }
        else if (post.Media.Count == 1)
        {
            var media = post.Media[0];
            var isPhoto = media.Type == MediaType.Photo;
            method = isPhoto ? "sendPhoto" : "sendVideo";
            result = await CallAsync(api, method, () =>
            {
                var form = new MultipartFormDataContent { { new StringContent(chat), "chat_id" }, { FilePart(media.Path), isPhoto ? "photo" : "video", Path.GetFileName(media.Path) } };
                if (text.Length > 0)
                {
                    form.Add(new StringContent(text), "caption");
                }

                if (!isPhoto)
                {
                    form.Add(new StringContent("true"), "supports_streaming");
                }

                return form;
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            method = "sendMediaGroup";
            result = await CallAsync(api, method, () =>
            {
                var items = new JsonArray();
                var form = new MultipartFormDataContent { { new StringContent(chat), "chat_id" } };
                for (var i = 0; i < post.Media.Count; i++)
                {
                    var name = "file" + i.ToString(CultureInfo.InvariantCulture);
                    var item = new JsonObject { ["type"] = post.Media[i].Type == MediaType.Photo ? "photo" : "video", ["media"] = "attach://" + name };
                    if (i == 0 && text.Length > 0)
                    {
                        item["caption"] = text;
                    }

                    items.Add(item);
                    form.Add(FilePart(post.Media[i].Path), name, Path.GetFileName(post.Media[i].Path));
                }

                form.Add(new StringContent(items.ToJsonString()), "media");
                return form;
            }, cancellationToken).ConfigureAwait(false);
        }

        var message = result is JsonArray list ? list.FirstOrDefault() : result;
        var id = JsonSafe.Str(message, "message_id");
        var username = JsonSafe.Str(message?["chat"], "username");
        progress?.Report(1);
        Logger.LogInformation("Telegram {Method} done (message {MessageId})", method, id);
        return new PublishResult(id, username.Length > 0 ? $"https://t.me/{username}/{id}" : string.Empty, TargetStatus.Published);
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body) => new PlatformRejectedException($"Telegram {step} failed (HTTP {status}): {PlatformText.Short(body)}");

    private async Task<JsonNode?> CallAsync(string api, string method, Func<HttpContent> content, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, api + method) { Content = content() }, null, null, cancellationToken).ConfigureAwait(false);
            if (reply.Status == 429 && attempt < 3)
            {
                // Telegram says how long to wait (parameters.retry_after); one message per second per chat is its documented pace.
                var seconds = JsonSafe.TryParse(reply.Body)?["parameters"]?["retry_after"]?.GetValue<int>() ?? 5;
                await Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 60)), cancellationToken).ConfigureAwait(false);
                continue;
            }

            return TelegramApi.Result(reply, method);
        }
    }
}
