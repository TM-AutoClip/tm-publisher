namespace TMPublisher.Core.Posts;

/// <summary>A local file attached to a post. The file stays where the user keeps it; only the path is stored.</summary>
public sealed record MediaItem(string Path, MediaType Type, long Bytes);
