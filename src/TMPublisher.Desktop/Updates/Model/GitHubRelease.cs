using System.Text.Json.Serialization;

namespace TMPublisher.Desktop.Updates;

/// <summary>The part of GitHub's release answer that is read.</summary>
public sealed class GitHubRelease
{
    /// <summary>The tag (for example <c>v0.1.7</c>).</summary>
    [JsonPropertyName("tag_name")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>The release's page.</summary>
    [JsonPropertyName("html_url")]
    public string Page { get; set; } = string.Empty;

    /// <summary>A draft (never offered).</summary>
    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    /// <summary>A pre-release (never offered).</summary>
    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }
}
