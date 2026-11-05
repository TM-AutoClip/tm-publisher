namespace TMPublisher.Runtime.Ipc;

/// <summary>The background scheduler's answer to <see cref="SchedulerCommands.Status"/>.</summary>
public sealed class SchedulerState
{
    /// <summary>Its version (it must match the window's).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>What it is doing.</summary>
    public SchedulerStatus Status { get; set; }

    /// <summary>Whether a post is being sent now.</summary>
    public bool Sending { get; set; }

    /// <summary>Time of the next scheduled post (UTC), when known.</summary>
    public DateTimeOffset? NextPostUtc { get; set; }

    /// <summary>The reason of an error (no secrets, no folders).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Process id (for the window's "stop" when the channel does not answer).</summary>
    public int ProcessId { get; set; }
}
