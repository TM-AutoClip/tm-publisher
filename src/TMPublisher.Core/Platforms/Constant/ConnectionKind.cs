namespace TMPublisher.Core.Platforms;

/// <summary>How an account is connected with the user's own developer app or key.</summary>
public enum ConnectionKind
{
    /// <summary>Browser sign-in that returns to <c>http://127.0.0.1:port</c> on this computer, with PKCE.</summary>
    LoopbackPkce = 0,

    /// <summary>Browser sign-in; the user pastes the address the browser ended on (https redirect).</summary>
    PastedRedirect = 1,

    /// <summary>The user pastes a token made by the platform's token tool.</summary>
    PastedToken = 2,

    /// <summary>Handle + app password.</summary>
    AppPassword = 3,

    /// <summary>The app registers the user's own app on the user's server, then a pasted code.</summary>
    RegisteredApp = 4,

    /// <summary>A secret address (webhook).</summary>
    SecretUrl = 5,

    /// <summary>A bot token + chat id.</summary>
    BotToken = 6,
}
