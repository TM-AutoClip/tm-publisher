namespace TMPublisher.Background.Triggers;

/// <summary>
/// The operating system's scheduler entry that starts the program with <c>--run-due</c> while it is closed: a Task Scheduler task of the
/// current user on Windows, a systemd user timer on Linux (an XDG autostart entry when there is no systemd user session). No administrator
/// rights. One entry per data folder; replaced in tests (no test touches the real scheduler except under a test name).
/// </summary>
public interface ISystemTrigger
{
    /// <summary>What the entry is called in the system, for the user ("Task Scheduler", "systemd user timer").</summary>
    string Kind { get; }

    /// <summary>The definition written for <paramref name="plan"/> (an unchanged definition is not written again).</summary>
    string Describe(TriggerPlan plan);

    /// <summary>Writes (or replaces) the entry.</summary>
    Task<TriggerResult> ApplyAsync(TriggerPlan plan, CancellationToken cancellationToken);

    /// <summary>Removes the entry of <paramref name="name"/> (nothing happens when there is none).</summary>
    Task<TriggerResult> RemoveAsync(string name, CancellationToken cancellationToken);

    /// <summary>Whether the entry of <paramref name="name"/> exists.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken);
}
