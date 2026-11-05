namespace TMPublisher.Runtime.Ipc;

/// <summary>
/// The lines the window sends to the background scheduler (one request line, one answer line; <see cref="Watch"/> keeps the connection open
/// and the scheduler writes <see cref="Changed"/> and <see cref="Notify"/> lines on it).
/// </summary>
public static class SchedulerCommands
{
    /// <summary>Answer: the state as JSON.</summary>
    public const string Status = "status";

    /// <summary>Read the schedule (and the settings) again.</summary>
    public const string Wake = "wake";

    /// <summary>The database was replaced or its protection changed: open it again.</summary>
    public const string Reload = "reload";

    /// <summary>The database key (base64) for a master-password database; never logged.</summary>
    public const string Key = "key";

    /// <summary>Pause sending.</summary>
    public const string Pause = "pause";

    /// <summary>Resume sending.</summary>
    public const string Resume = "resume";

    /// <summary>End the scheduler process when no post is being sent (answer <see cref="Busy"/> otherwise).</summary>
    public const string Quit = "quit";

    /// <summary>Keep the connection open for events.</summary>
    public const string Watch = "watch";

    /// <summary>Answer: done.</summary>
    public const string Ok = "ok";

    /// <summary>Answer: a post is being sent now.</summary>
    public const string Busy = "busy";

    /// <summary>Answer prefix: failed, the reason follows.</summary>
    public const string Error = "error";

    /// <summary>Event: posts changed state.</summary>
    public const string Changed = "changed";

    /// <summary>Event prefix: a notification as JSON follows.</summary>
    public const string Notify = "notify";
}
