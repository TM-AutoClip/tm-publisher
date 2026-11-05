namespace TMPublisher.Scheduling;

/// <summary>Operating-system events that make the schedule stale: waking from sleep, a clock or time-zone change.</summary>
public interface IPowerEvents
{
    /// <summary>The clock or the time zone changed.</summary>
    event EventHandler? ScheduleStale;

    /// <summary>The computer woke up from sleep (the network may still be reconnecting).</summary>
    event EventHandler? Resumed;

    /// <summary>Starts listening (no-op when the system offers no such events).</summary>
    Task StartAsync(CancellationToken cancellationToken);
}
