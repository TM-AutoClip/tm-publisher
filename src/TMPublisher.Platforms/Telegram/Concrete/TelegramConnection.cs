using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;
using TMPublisher.Platforms.Connections;

namespace TMPublisher.Platforms.Telegram;

/// <summary>Bot token + chat: the test reads the bot (getMe) and the chat (getChat); the chat's numeric id is stored.</summary>
public sealed class TelegramConnection(HttpTransport transport, TimeProvider clock) : BotTokenConnectionBase(transport, clock)
{
    /// <inheritdoc />
    protected override async Task<AccountIdentity> ReadIdentityAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var api = TelegramApi.Base(credentials.Require(CredentialKeys.BotToken));
        var bot = TelegramApi.Result(await Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, api + "getMe"), cancellationToken).ConfigureAwait(false), "getMe");
        var chat = TelegramApi.Result(await Http.SendAsync(new HttpRequestMessage(HttpMethod.Get,
            api + "getChat?chat_id=" + Uri.EscapeDataString(credentials.Require(CredentialKeys.ChatId))), cancellationToken).ConfigureAwait(false), "getChat");
        var id = JsonSafe.Str(chat, "id");
        if (id.Length == 0)
        {
            throw new PlatformRejectedException("Telegram did not return the chat id.");
        }

        var title = JsonSafe.Str(chat, "title") is { Length: > 0 } t ? t : JsonSafe.Str(chat, "username");
        var username = JsonSafe.Str(chat, "username");
        return new AccountIdentity(id, title + " (@" + JsonSafe.Str(bot, "username") + ")", username.Length > 0 ? "@" + username : null);
    }
}
