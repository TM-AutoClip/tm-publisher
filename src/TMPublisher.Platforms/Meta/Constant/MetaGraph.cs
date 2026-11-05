namespace TMPublisher.Platforms.Meta;

/// <summary>Meta Graph API version and roots in one place (Facebook Page and Instagram through Facebook Login).</summary>
public static class MetaGraph
{
    /// <summary>Graph API version.</summary>
    public const string Version = "v26.0";

    /// <summary>Graph root.</summary>
    public const string Root = "https://graph.facebook.com/" + Version + "/";

    /// <summary>Video upload root (Facebook Page videos).</summary>
    public const string VideoRoot = "https://graph-video.facebook.com/" + Version + "/";
}
