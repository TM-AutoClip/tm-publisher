namespace TMPublisher.Platforms.TikTok;

/// <summary>TikTok Open API addresses (v2).</summary>
public static class TikTokApi
{
    /// <summary>API root with version.</summary>
    public const string Root = "https://open.tiktokapis.com/v2/";

    /// <summary>Token endpoint.</summary>
    public const string Token = Root + "oauth/token/";

    /// <summary>Sign-in page.</summary>
    public const string Authorize = "https://www.tiktok.com/v2/auth/authorize/";

    /// <summary>Permissions: basic profile, upload to the inbox (draft), direct post.</summary>
    public const string Scopes = "user.info.basic,video.upload,video.publish";
}
