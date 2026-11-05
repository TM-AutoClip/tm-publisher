using TMPublisher.Core.Posts;
using TMPublisher.Core.Text;

namespace TMPublisher.Core.Platforms;

/// <summary>
/// What a platform accepts. The compose screen greys out what is not supported (with the reason), the validator blocks it, and the
/// publisher still checks again before any request.
/// </summary>
public sealed record PlatformCapabilities
{
    /// <summary>Supported post kinds.</summary>
    public required IReadOnlySet<PostKind> Kinds { get; init; }

    /// <summary>Longest text; 0 = no text field.</summary>
    public required int MaxTextLength { get; init; }

    /// <summary>Longest text of a post with media (a caption); 0 = same as <see cref="MaxTextLength"/>.</summary>
    public int MaxMediaTextLength { get; init; }

    /// <summary>How the platform counts text length.</summary>
    public required ITextMeasure TextMeasure { get; init; }

    /// <summary>Has a separate title field.</summary>
    public bool HasTitle { get; init; }

    /// <summary>Longest title (characters).</summary>
    public int MaxTitleLength { get; init; }

    /// <summary>Has a separate keyword (tag) field (YouTube).</summary>
    public bool HasKeywords { get; init; }

    /// <summary>Most hashtags in the text; 0 = no limit known.</summary>
    public int MaxHashtags { get; init; }

    /// <summary>Most photos in one post.</summary>
    public int MaxPhotos { get; init; }

    /// <summary>Most items in an album.</summary>
    public int MaxAlbumItems { get; init; }

    /// <summary>Largest photo (bytes); 0 = unknown.</summary>
    public long MaxPhotoBytes { get; init; }

    /// <summary>Largest video (bytes); 0 = unknown.</summary>
    public long MaxVideoBytes { get; init; }

    /// <summary>Supports the visibility option (public / unlisted / private).</summary>
    public bool SupportsPrivacy { get; init; }

    /// <summary>Known posting limits per account.</summary>
    public IReadOnlyList<QuotaRule> Quotas { get; init; } = [];

    /// <summary>Estimated cost of a post in US dollars (X); 0 = free.</summary>
    public decimal CostPerPost { get; init; }

    /// <summary>Estimated cost of a post that contains a link (X); 0 = same as <see cref="CostPerPost"/>.</summary>
    public decimal CostPerLinkPost { get; init; }

    /// <summary>Localization key that explains why a kind is not supported (shown next to the greyed account).</summary>
    public IReadOnlyDictionary<PostKind, string> UnsupportedReasons { get; init; } = new Dictionary<PostKind, string>();
}
