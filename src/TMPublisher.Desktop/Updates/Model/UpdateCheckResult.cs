namespace TMPublisher.Desktop.Updates;

/// <summary>
/// What a look found: the outcome, the newest published version (null when none is known), the page to download it from and, while GitHub
/// asks to wait, until when.
/// </summary>
public sealed record UpdateCheckResult(UpdateOutcome Outcome, Version? Latest, Uri Page, DateTimeOffset? NotBeforeUtc = null)
{
    /// <summary>Nothing looked yet.</summary>
    public static UpdateCheckResult None { get; } = new(UpdateOutcome.NotChecked, null, UpdateRules.DownloadPage);
}
