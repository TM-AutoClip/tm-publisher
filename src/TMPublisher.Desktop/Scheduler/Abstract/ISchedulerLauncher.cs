namespace TMPublisher.Desktop.Scheduler;

/// <summary>Starts the background scheduler process for this data folder (replaced in tests: no process is started there).</summary>
public interface ISchedulerLauncher
{
    /// <summary>Starts it; <c>false</c> with the reason when it could not be started (not found next to the window program, refused).</summary>
    bool TryStart(out string reason);
}
