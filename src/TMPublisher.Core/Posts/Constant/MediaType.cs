namespace TMPublisher.Core.Posts;

/// <summary>Type of a media file. Stored as a number: new values are appended at the end.</summary>
public enum MediaType
{
    /// <summary>Still image (JPEG, PNG, WebP, GIF).</summary>
    Photo = 0,

    /// <summary>Video (MP4, MOV, WebM, MKV).</summary>
    Video = 1,
}
