using Microsoft.Extensions.Logging;
using TMPublisher.Scheduling;

namespace TMPublisher.Desktop.Startup;

/// <summary>Starts the scheduler after unlock and connects the wake-up / clock-change events to it.</summary>
public sealed class SchedulerStartupTask(PostScheduler scheduler, IPowerEvents power, ILogger<SchedulerStartupTask> logger) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        power.ScheduleStale += (_, _) => scheduler.Wake();
        power.Resumed += (_, _) => scheduler.WakeAfterResume();
        try
        {
            await power.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Not fatal: without these events the scheduler still wakes at least once a day.
            logger.LogError(ex, "Could not listen for resume / clock events");
        }

        await scheduler.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}
