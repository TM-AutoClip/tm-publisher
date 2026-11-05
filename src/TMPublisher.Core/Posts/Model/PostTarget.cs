namespace TMPublisher.Core.Posts;

/// <summary>One account a post goes to, with the exact text that account receives.</summary>
public sealed class PostTarget
{
    /// <summary>Id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Post.</summary>
    public Guid PostId { get; set; }

    /// <summary>Account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Sending order (0 first).</summary>
    public int Order { get; set; }

    /// <summary>Final text sent to this account (composed in the form, shown to the user, sent as is).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Final title for platforms with a title field.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Keywords for platforms with a separate keyword field (YouTube tags), without '#'.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Per-target options (<see cref="TargetOptionKeys"/>).</summary>
    public Dictionary<string, string> Options { get; set; } = [];

    /// <summary>State.</summary>
    public TargetStatus Status { get; set; }

    /// <summary>The platform's id of the published item.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Address of the published item (when the platform gives one).</summary>
    public string? Url { get; set; }

    /// <summary>Last error or note shown to the user (never contains a token).</summary>
    public string? Message { get; set; }

    /// <summary>When it was published (UTC).</summary>
    public DateTimeOffset? PublishedAtUtc { get; set; }
}
