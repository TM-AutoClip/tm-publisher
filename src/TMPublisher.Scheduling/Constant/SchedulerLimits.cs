namespace TMPublisher.Scheduling;

/// <summary>Scheduler timings.</summary>
public static class SchedulerLimits
{
    /// <summary>A post this late (computer was on, e.g. busy sending another) is still sent on its own; later ones wait for the user.</summary>
    public static readonly TimeSpan LateTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Longest single wait: the schedule is read again at least this often even when nothing is due (one small database read). It covers a
    /// clock change the system did not report.
    /// </summary>
    public static readonly TimeSpan SafetyInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// While the window is open the timer's wait is cut into pieces of at most this length, and each piece compares the wall clock: a system
    /// timer does not count the time the computer slept, so a single long wait would fire that much late after waking (a post due soon after
    /// waking would be missed). A piece only looks at the clock; the schedule is read when the post's time came, the computer woke up or the
    /// clock jumped.
    /// </summary>
    public static readonly TimeSpan SleepCheck = TimeSpan.FromMinutes(1);

    /// <summary>The wait before a post whose claim failed twice in one run is tried again (never a fast loop).</summary>
    public static readonly TimeSpan ClaimRetry = TimeSpan.FromMinutes(1);

    /// <summary>At most this often the scheduler writes a sign of life into its log (only when it woke up anyway).</summary>
    public static readonly TimeSpan Heartbeat = TimeSpan.FromHours(1);

    /// <summary>
    /// How long a stop "only when idle" (a restore in the window) waits for a run that is reading or claiming the schedule to end; a run
    /// that is still sending after this answers "busy".
    /// </summary>
    public static readonly TimeSpan IdleStopWait = TimeSpan.FromSeconds(2);

    /// <summary>After waking from sleep, the wait before due posts are sent (Wi-Fi usually reconnects within a few seconds).</summary>
    public static readonly TimeSpan ResumeSettle = TimeSpan.FromSeconds(10);

    /// <summary>After waking from sleep, the longest wait for a network connection before due posts are sent anyway.</summary>
    public static readonly TimeSpan ResumeNetworkWait = TimeSpan.FromMinutes(2);

    /// <summary>How often the network is looked at during <see cref="ResumeNetworkWait"/> (only right after waking).</summary>
    public static readonly TimeSpan ResumeNetworkCheck = TimeSpan.FromSeconds(3);

    /// <summary>When the processing state of a sent video is checked again (after the previous check).</summary>
    public static readonly IReadOnlyList<TimeSpan> ProcessingChecks =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30)];

    /// <summary>Longest wait for another process's token refresh of the same account to finish.</summary>
    public static readonly TimeSpan RefreshLockWait = TimeSpan.FromMinutes(2);

    /// <summary>How often a held refresh lock is tried again.</summary>
    public static readonly TimeSpan RefreshLockRetry = TimeSpan.FromMilliseconds(200);
}
