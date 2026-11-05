using System.Text.Json.Nodes;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Telegram;

/// <summary>Bot API address and answer envelope. The token is part of the address, so the address is never logged (the redactor masks it too).</summary>
public static class TelegramApi
{
    /// <summary>Bot API root of a token.</summary>
    public static string Base(string token) => "https://api.telegram.org/bot" + token + "/";

    /// <summary>The <c>result</c> of an answer, or the classified error.</summary>
    public static JsonNode? Result(PlatformReply reply, string method)
    {
        var root = JsonSafe.TryParse(reply.Body);
        if (reply.IsSuccess && root?["ok"]?.GetValue<bool>() == true)
        {
            return root["result"];
        }

        var description = JsonSafe.Str(root, "description");
        var text = $"Telegram {method} failed (HTTP {reply.Status}): {(description.Length > 0 ? description : PlatformText.Short(reply.Body))}";
        return reply.Status switch
        {
            401 or 404 => throw new PlatformAuthException(text + " The bot token is not valid."),
            403 => throw new PlatformRejectedException(text + " Make the bot an administrator of the channel with the right to post."),
            429 => throw new PlatformQuotaException(text),
            _ => throw new PlatformRejectedException(text),
        };
    }
}
