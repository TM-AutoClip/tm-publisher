namespace TMPublisher.Core.Posts;

/// <summary>Keys of the per-target options (stored as text; values are invariant strings).</summary>
public static class TargetOptionKeys
{
    /// <summary>Visibility: <c>public</c>, <c>unlisted</c> or <c>private</c> (platforms that support it).</summary>
    public const string Privacy = "privacy";

    /// <summary>TikTok: <c>draft</c> (inbox, default) or <c>direct</c> (direct post, private on unaudited apps).</summary>
    public const string TikTokMode = "tiktok.mode";

    /// <summary>Pinterest board id.</summary>
    public const string PinterestBoard = "pinterest.board";

    /// <summary>Pinterest destination link (optional).</summary>
    public const string PinterestLink = "pinterest.link";

    /// <summary>YouTube category id (default 22, People &amp; Blogs).</summary>
    public const string YouTubeCategory = "youtube.category";

    /// <summary>YouTube "made for kids" (<c>true</c>/<c>false</c>).</summary>
    public const string MadeForKids = "youtube.madeForKids";

    /// <summary>The media is AI-generated / synthetic (<c>true</c>/<c>false</c>); sent where the platform has a field for it.</summary>
    public const string Synthetic = "synthetic";
}
