namespace TMPublisher.Background.Triggers;

/// <summary>
/// What was last written to the system scheduler (<c>system-trigger.json</c> in the data folder, no secrets): shown in Settings, compared so an
/// unchanged entry is not written again, and read by a run that cannot open the database (a master password, a keyring that does not answer):
/// it knows when the posts are due and sets the next start without the database.
/// </summary>
public sealed class TriggerRecord
{
    /// <summary>State.</summary>
    public TriggerState State { get; set; }

    /// <summary>The system scheduler's kind ("Task Scheduler", …).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The task / unit name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the system starts the program next (UTC); <c>null</c> when only at sign-in.</summary>
    public DateTimeOffset? NextRunUtc { get; set; }

    /// <summary>The earliest scheduled post's time when it was written (UTC).</summary>
    public DateTimeOffset? NextPostUtc { get; set; }

    /// <summary>
    /// The times of the next scheduled posts when it was written (UTC, earliest first, at most <see cref="TriggerLimits.KnownPosts"/>): a run
    /// that cannot open a master-password database notices each one that is due and sets the next start for the next one.
    /// </summary>
    public List<DateTimeOffset> PostTimesUtc { get; set; } = [];

    /// <summary>Whether the computer is woken from sleep for the starts (kept so a run without the database writes the same option).</summary>
    public bool WakeToRun { get; set; }

    /// <summary>The written definition.</summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>The reason of a failure or the warning of a fallback (English).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>When it was written (UTC).</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
