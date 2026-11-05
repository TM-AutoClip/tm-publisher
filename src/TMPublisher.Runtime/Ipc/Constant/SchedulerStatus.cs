namespace TMPublisher.Runtime.Ipc;

/// <summary>What the background scheduler is doing.</summary>
public enum SchedulerStatus
{
    /// <summary>Not running (no answer on its channel).</summary>
    NotRunning = 0,

    /// <summary>Opening the database.</summary>
    Starting = 1,

    /// <summary>No database yet (first run not finished in the window).</summary>
    NoDatabase = 2,

    /// <summary>The database is protected with a master password: waits until the window is unlocked and hands the key over.</summary>
    WaitingForKey = 3,

    /// <summary>Sends posts at their time.</summary>
    Running = 4,

    /// <summary>Running, but sending is paused by the user.</summary>
    Paused = 5,

    /// <summary>The database could not be opened (the reason is in the message and the log); tried again at the next wake-up.</summary>
    Error = 6,
}
