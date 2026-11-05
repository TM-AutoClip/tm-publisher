namespace TMPublisher.Scheduling;

/// <summary>Scheduler timings.</summary>
public static class SchedulerLimits
{
    /// <summary>A post this late (computer was on, e.g. busy sending another) is still sent on its own; later ones wait for the user.</summary>
    public static readonly TimeSpan LateTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Longest single wait; a longer wait is cut into such pieces (one wake-up a day).</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromHours(24);

    /// <summary>After waking from sleep, the wait before due posts are sent (Wi-Fi usually reconnects within a few seconds).</summary>
    public static readonly TimeSpan ResumeSettle = TimeSpan.FromSeconds(10);

    /// <summary>After waking from sleep, the longest wait for a network connection before due posts are sent anyway.</summary>
    public static readonly TimeSpan ResumeNetworkWait = TimeSpan.FromMinutes(2);

    /// <summary>How often the network is looked at during <see cref="ResumeNetworkWait"/> (only right after waking).</summary>
    public static readonly TimeSpan ResumeNetworkCheck = TimeSpan.FromSeconds(3);

    /// <summary>When the processing state of a sent video is checked again (after the previous check).</summary>
    public static readonly IReadOnlyList<TimeSpan> ProcessingChecks =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30)];
}
