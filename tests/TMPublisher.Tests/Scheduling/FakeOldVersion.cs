using TMPublisher.Core.Instance;

namespace TMPublisher.Tests.Scheduling;

/// <summary>An old TM Publisher that runs or not, as the test says.</summary>
internal sealed class FakeOldVersion : IOldVersionCheck
{
    public bool Runs { get; set; }

    public bool OldVersionRuns() => Runs;
}
