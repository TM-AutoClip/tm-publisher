namespace TMPublisher.Core.Posts;

/// <summary>
/// A post: one content sent to several targets in the user's order. Each target keeps its own final text (what the form showed is what
/// is published; nothing is merged at send time).
/// </summary>
public sealed class Post
{
    /// <summary>Id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kind of content.</summary>
    public PostKind Kind { get; set; }

    /// <summary>Title (used by platforms with a title field; the composer puts it on the first line elsewhere).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Body text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Tags added for this post only (on top of each platform's default set).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Media files in order.</summary>
    public List<MediaItem> Media { get; set; } = [];

    /// <summary>When to send (UTC); <c>null</c> for a draft or "now".</summary>
    public DateTimeOffset? ScheduledAtUtc { get; set; }

    /// <summary>State.</summary>
    public PostStatus Status { get; set; }

    /// <summary>The group the targets came from (<c>null</c> when the accounts were picked by hand).</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Targets in sending order.</summary>
    public List<PostTarget> Targets { get; set; } = [];
}
