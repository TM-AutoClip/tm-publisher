namespace TMPublisher.Background.Triggers;

/// <summary>Timings of the system scheduler's starts.</summary>
public static class TriggerLimits
{
    /// <summary>
    /// After the next post's time, the run is started again this often until a run sets the next start (it covers a run that ended without
    /// setting it, for example when the computer was switched off while it sent).
    /// </summary>
    public static readonly TimeSpan SafetyInterval = TimeSpan.FromMinutes(30);

    /// <summary>A run is stopped by the system when it takes longer than this (a hung upload must not keep a process for days).</summary>
    public static readonly TimeSpan RunTimeLimit = TimeSpan.FromMinutes(60);

    /// <summary>The start after signing in waits this long (the network and the keyring are usually ready by then).</summary>
    public static readonly TimeSpan SignInDelay = TimeSpan.FromMinutes(1);

    /// <summary>A next start that is due already is set this far ahead (a start in the past would not run, or run at once in a loop).</summary>
    public static readonly TimeSpan SoonestStart = TimeSpan.FromMinutes(1);

    /// <summary>How long a run waits for the sender lock when a 0.1.3–0.1.4 scheduler was asked to end.</summary>
    public static readonly TimeSpan OldSchedulerWait = TimeSpan.FromSeconds(20);
}
