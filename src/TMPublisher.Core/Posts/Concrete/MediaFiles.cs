namespace TMPublisher.Core.Posts;

/// <summary>Which local files can be attached and what kind of post a set of files makes.</summary>
public static class MediaFiles
{
    /// <summary>Photo extensions.</summary>
    public static IReadOnlyList<string> PhotoExtensions { get; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    /// <summary>Video extensions.</summary>
    public static IReadOnlyList<string> VideoExtensions { get; } = [".mp4", ".mov", ".m4v", ".webm"];

    /// <summary>File picker patterns ("*.jpg"...).</summary>
    public static IReadOnlyList<string> Patterns { get; } = [.. PhotoExtensions.Concat(VideoExtensions).Select(e => "*" + e)];

    /// <summary>Photo, video, or <c>null</c> for any other file.</summary>
    public static MediaType? TypeOf(string path)
    {
        var extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        return PhotoExtensions.Contains(extension) ? MediaType.Photo : VideoExtensions.Contains(extension) ? MediaType.Video : null;
    }

    /// <summary>
    /// The kind of post: no file = text, one video = video, only photos = photo(s), anything else (photos with a video, several videos) =
    /// album (mixed carousel).
    /// </summary>
    public static PostKind KindOf(IReadOnlyCollection<MediaItem> media)
    {
        ArgumentNullException.ThrowIfNull(media);
        var videos = media.Count(m => m.Type == MediaType.Video);
        return media.Count == 0 ? PostKind.Text
            : videos == 0 ? PostKind.Photo
            : videos == 1 && media.Count == 1 ? PostKind.Video
            : PostKind.Album;
    }
}
