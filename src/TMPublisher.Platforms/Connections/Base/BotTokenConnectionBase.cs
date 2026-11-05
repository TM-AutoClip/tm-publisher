using TMPublisher.Core.Accounts;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Connections;

/// <summary>A bot token plus the chat it posts to (Telegram).</summary>
public abstract class BotTokenConnectionBase(HttpTransport transport, TimeProvider clock) : ConnectionMethodBase(transport, clock)
{
    /// <inheritdoc />
    public override ConnectionKind Kind => ConnectionKind.BotToken;

    /// <inheritdoc />
    public override IReadOnlyList<ConnectionField> Fields =>
    [
        new(CredentialKeys.BotToken, "field.bot_token", Secret: true),
        new(CredentialKeys.ChatId, "field.chat_id", HintKey: "field.chat_id.hint"),
    ];

    /// <inheritdoc />
    protected override Task<Credentials> AuthorizeAsync(ConnectionInput input, IConnectionPrompt prompt, CancellationToken cancellationToken) =>
        Task.FromResult(FromInput(input, CredentialKeys.BotToken, CredentialKeys.ChatId));
}
