namespace TMPublisher.Scheduling;

/// <summary>Operating-system events that make the schedule stale: waking from sleep, a clock or time-zone change.</summary>
public interface IPowerEvents
{
    /// <summary>The computer woke up or the clock changed.</summary>
    event EventHandler? ScheduleStale;

    /// <summary>Starts listening (no-op when the system offers no such events).</summary>
    Task StartAsync(CancellationToken cancellationToken);
}
