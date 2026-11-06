namespace TMPublisher.Background.Triggers;

/// <summary>Timings of the system scheduler's starts.</summary>
public static class TriggerLimits
{
    /// <summary>
    /// After the next post's time the program is started again at these moments (counted from that time), in case the run at that time ended
    /// before it set the next start (the computer was switched off while it sent, the run was stopped). A run that sets the next start
    /// replaces them. Fixed moments, not a repetition: a repeating or "after the last start" timer can fire at once when it is written again.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> SafetyStarts = [TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(120)];

    /// <summary>A run is stopped by the system when it takes longer than this (a hung upload must not keep a process for days).</summary>
    public static readonly TimeSpan RunTimeLimit = TimeSpan.FromMinutes(60);

    /// <summary>Windows: the start after signing in waits this long (the network and the keyring are usually ready by then).</summary>
    public static readonly TimeSpan SignInDelay = TimeSpan.FromMinutes(1);

    /// <summary>A next start that is due already is set this far ahead (a start in the past would not run, or run at once in a loop).</summary>
    public static readonly TimeSpan SoonestStart = TimeSpan.FromMinutes(1);

    /// <summary>How many of the next post times the record keeps for a run that cannot open the database.</summary>
    public const int KnownPosts = 20;

    /// <summary>How long a run waits for the sender lock when a 0.1.3–0.1.4 scheduler was asked to end.</summary>
    public static readonly TimeSpan OldSchedulerWait = TimeSpan.FromSeconds(20);
}
