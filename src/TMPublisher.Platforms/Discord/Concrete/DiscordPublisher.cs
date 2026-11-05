using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Discord;

/// <summary>
/// Webhook post: <c>POST {webhook}?wait=true</c>; text as JSON, files as multipart (<c>payload_json</c> + <c>files[n]</c>). With
/// <c>wait=true</c> Discord returns the message; its address needs the server id, which a REST message does not carry, so it is read from
/// the webhook. 429 → waits <c>retry_after</c> once or twice.
/// </summary>
public sealed class DiscordPublisher(HttpTransport transport, ILogger<DiscordPublisher> logger) : PublisherBase(transport, TimeSpan.FromMinutes(10), logger)
{
    /// <inheritdoc />
    public override async Task<PublishResult> PublishAsync(PublishRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hook = request.Credentials.Require(CredentialKeys.WebhookUrl);
        var post = request.Post;
        var total = post.Media.Sum(m => CheckFile(m, DiscordPlatform.MaxFileBytes, "Discord").Length);
        if (total > DiscordPlatform.MaxRequestBytes)
        {
            throw new PlatformRejectedException($"Discord accepts up to 25 MB per post; these files are {total / 1048576.0:0.#} MB together.");
        }

        var payload = new JsonObject { ["content"] = request.Target.Text, ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() } };
        HttpContent Build()
        {
            if (post.Media.Count == 0)
            {
                return PlatformHttp.JsonBody(payload);
            }

            var form = new MultipartFormDataContent { { new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), "payload_json" } };
            for (var i = 0; i < post.Media.Count; i++)
            {
                form.Add(FilePart(post.Media[i].Path), "files[" + i.ToString(CultureInfo.InvariantCulture) + "]", Path.GetFileName(post.Media[i].Path));
            }

            return form;
        }

        progress?.Report(0.05);
        for (var attempt = 1; ; attempt++)
        {
            var reply = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, hook + "?wait=true") { Content = Build() }, null, null, cancellationToken).ConfigureAwait(false);
            if (reply.Status == 429 && attempt < 3)
            {
                var seconds = double.TryParse(JsonSafe.Str(JsonSafe.TryParse(reply.Body), "retry_after"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 2;
                await Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0.5, 60)), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!reply.IsSuccess)
            {
                throw Classify("post", reply.Status, reply.Body);
            }

            var message = JsonSafe.TryParse(reply.Body);
            var id = JsonSafe.Str(message, "id");
            var channel = JsonSafe.Str(message, "channel_id");
            var guild = JsonSafe.Str(message, "guild_id");
            if (guild.Length == 0 && id.Length > 0)
            {
                // A REST message has no guild_id; the webhook itself carries it (read without a token, the address is the key).
                guild = await ReadAfterPublishAsync("Discord server id", id, async ct => JsonSafe.Str(
                    await SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, hook), null, "webhook read", ct).ConfigureAwait(false), "guild_id"),
                    cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(1);
            Logger.LogInformation("Discord webhook message {MessageId} posted", id);
            return new PublishResult(id, guild.Length > 0 ? $"https://discord.com/channels/{guild}/{channel}/{id}" : string.Empty, TargetStatus.Published);
        }
    }

    /// <inheritdoc />
    protected override Exception Classify(string step, int status, string body)
    {
        var text = $"Discord {step} failed (HTTP {status}): {(JsonSafe.Str(JsonSafe.TryParse(body), "message") is { Length: > 0 } m ? m : PlatformText.Short(body))}";
        return status switch
        {
            401 or 404 => new PlatformAuthException(text + " The webhook was deleted or its address is wrong."),
            429 => new PlatformQuotaException(text),
            _ => new PlatformRejectedException(text),
        };
    }
}
