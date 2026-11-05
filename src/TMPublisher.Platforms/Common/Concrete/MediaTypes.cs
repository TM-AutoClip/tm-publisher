namespace TMPublisher.Platforms.Common;

/// <summary>Content types of local media files by extension.</summary>
public static class MediaTypes
{
    /// <summary>The MIME type of the file (images and videos; anything else is <c>application/octet-stream</c>).</summary>
    public static string Of(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".JPG" or ".JPEG" => "image/jpeg",
        ".PNG" => "image/png",
        ".WEBP" => "image/webp",
        ".GIF" => "image/gif",
        ".MP4" or ".M4V" => "video/mp4",
        ".MOV" => "video/quicktime",
        ".WEBM" => "video/webm",
        ".MKV" => "video/x-matroska",
        _ => "application/octet-stream",
    };
}
