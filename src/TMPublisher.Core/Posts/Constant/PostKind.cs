namespace TMPublisher.Core.Posts;

/// <summary>What a post carries. Stored as a number: new values are appended at the end.</summary>
public enum PostKind
{
    /// <summary>Text only.</summary>
    Text = 0,

    /// <summary>One or more photos.</summary>
    Photo = 1,

    /// <summary>One video.</summary>
    Video = 2,

    /// <summary>Photos and videos mixed (album / carousel).</summary>
    Album = 3,
}
