namespace TMPublisher.Platforms.Pinterest;

/// <summary>Pinterest API v5 addresses and keys.</summary>
public static class PinterestApi
{
    /// <summary>API root.</summary>
    public const string Root = "https://api.pinterest.com/v5/";

    /// <summary>Sign-in page.</summary>
    public const string Authorize = "https://www.pinterest.com/oauth/";

    /// <summary>Permissions.</summary>
    public const string Scopes = "boards:read,pins:read,pins:write,user_accounts:read";

    /// <summary>Credential key of the board pins go to.</summary>
    public const string BoardKey = "pinterest_board";
}
