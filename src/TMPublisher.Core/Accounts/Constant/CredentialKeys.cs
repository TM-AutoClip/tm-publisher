namespace TMPublisher.Core.Accounts;

/// <summary>Keys of <see cref="Credentials"/>.</summary>
public static class CredentialKeys
{
    /// <summary>Client id / app id / client key of the user's own developer app.</summary>
    public const string ClientId = "client_id";

    /// <summary>Client secret / app secret of the user's own developer app.</summary>
    public const string ClientSecret = "client_secret";

    /// <summary>Redirect (callback) address registered in the user's app.</summary>
    public const string RedirectUri = "redirect_uri";

    /// <summary>Access token.</summary>
    public const string AccessToken = "access_token";

    /// <summary>Refresh token.</summary>
    public const string RefreshToken = "refresh_token";

    /// <summary>Access token expiry as Unix seconds (UTC).</summary>
    public const string AccessExpiresAt = "access_expires_at";

    /// <summary>Refresh token expiry as Unix seconds (UTC).</summary>
    public const string RefreshExpiresAt = "refresh_expires_at";

    /// <summary>A user token pasted from a token tool (Meta Graph API Explorer, LinkedIn token generator).</summary>
    public const string UserToken = "user_token";

    /// <summary>Page id (Facebook / Instagram).</summary>
    public const string PageId = "page_id";

    /// <summary>Server / instance address (Mastodon, Bluesky PDS).</summary>
    public const string Server = "server";

    /// <summary>Handle or login name (Bluesky).</summary>
    public const string Handle = "handle";

    /// <summary>App password (Bluesky).</summary>
    public const string AppPassword = "app_password";

    /// <summary>Bot token (Telegram).</summary>
    public const string BotToken = "bot_token";

    /// <summary>Chat / channel id (Telegram).</summary>
    public const string ChatId = "chat_id";

    /// <summary>Webhook address (Discord).</summary>
    public const string WebhookUrl = "webhook_url";

    /// <summary>The platform user id the tokens belong to.</summary>
    public const string UserId = "user_id";

    /// <summary>Loopback port registered in the user's app.</summary>
    public const string Port = "port";
}
