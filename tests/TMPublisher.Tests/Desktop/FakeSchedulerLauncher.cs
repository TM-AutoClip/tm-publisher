using TMPublisher.Desktop.Scheduler;

namespace TMPublisher.Tests.Desktop;

/// <summary>Counts start requests instead of starting the background scheduler process.</summary>
internal sealed class FakeSchedulerLauncher : ISchedulerLauncher
{
    public int Starts { get; private set; }

    public bool TryStart(out string reason)
    {
        Starts++;
        reason = "not started in tests";
        return false;
    }
}
