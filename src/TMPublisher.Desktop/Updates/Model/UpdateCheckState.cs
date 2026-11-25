namespace TMPublisher.Desktop.Updates;

/// <summary>
/// The last answer of GitHub (<see cref="UpdateRules.StateFileName"/> in the data folder): its ETag (the next start sends it, and an unchanged
/// answer, 304, does not count against GitHub's hourly limit), the newest release it named and until when GitHub asked to wait.
/// </summary>
public sealed class UpdateCheckState
{
    /// <summary>When GitHub last answered (UTC); null = never.</summary>
    public DateTimeOffset? CheckedAtUtc { get; set; }

    /// <summary>GitHub's ETag of that answer; null when there is none.</summary>
    public string? ETag { get; set; }

    /// <summary>The tag of the newest published release in that answer (for example <c>v0.1.7</c>); null = none.</summary>
    public string? LatestTag { get; set; }

    /// <summary>The page of that release on GitHub; null = none.</summary>
    public string? LatestPage { get; set; }

    /// <summary>No request before this time (GitHub's rate limit said so); null = none.</summary>
    public DateTimeOffset? NotBeforeUtc { get; set; }
}
