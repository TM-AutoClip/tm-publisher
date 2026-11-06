namespace TMPublisher.Runtime.Ipc;

/// <summary>What a 0.1.3–0.1.4 background scheduler answered to "quit".</summary>
public enum OldSchedulerAnswer
{
    /// <summary>Nothing listened: no old scheduler runs for the data folder.</summary>
    NotRunning,

    /// <summary>It ends now.</summary>
    Quitting,

    /// <summary>It is sending a post; asked again later.</summary>
    Busy,

    /// <summary>Something answered that is not an old scheduler.</summary>
    Unexpected,
}
