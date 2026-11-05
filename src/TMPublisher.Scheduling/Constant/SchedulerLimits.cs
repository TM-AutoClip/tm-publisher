namespace TMPublisher.Scheduling;

/// <summary>Scheduler timings.</summary>
public static class SchedulerLimits
{
    /// <summary>A post this late (computer was on, e.g. busy sending another) is still sent on its own; later ones wait for the user.</summary>
    public static readonly TimeSpan LateTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Longest single wait; a longer wait is cut into such pieces (one wake-up a day).</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromHours(24);

    /// <summary>When the processing state of a sent video is checked again (after the previous check).</summary>
    public static readonly IReadOnlyList<TimeSpan> ProcessingChecks =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30)];
}
