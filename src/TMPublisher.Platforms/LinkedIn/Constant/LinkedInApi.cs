namespace TMPublisher.Platforms.LinkedIn;

/// <summary>
/// LinkedIn API addresses. The REST API needs a monthly version header (<c>LinkedIn-Version: YYYYMM</c>); LinkedIn retires a version after
/// about a year, so this value must be moved forward at least once a year.
/// </summary>
public static class LinkedInApi
{
    /// <summary>REST version (YYYYMM).</summary>
    public const string Version = "202609";

    /// <summary>REST root.</summary>
    public const string Rest = "https://api.linkedin.com/rest/";

    /// <summary>OpenID user info.</summary>
    public const string UserInfo = "https://api.linkedin.com/v2/userinfo";
}
