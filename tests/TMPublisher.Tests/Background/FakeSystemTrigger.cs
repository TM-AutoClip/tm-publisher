using TMPublisher.Background.Triggers;

namespace TMPublisher.Tests.Background;

/// <summary>Records the plans instead of writing them to the system scheduler (no test touches the real one except under a test name).</summary>
internal sealed class FakeSystemTrigger : ISystemTrigger
{
    private readonly HashSet<string> _existing = [];

    public List<TriggerPlan> Applied { get; } = [];

    public List<string> Removed { get; } = [];

    public string Kind => "test scheduler";

    public string Describe(TriggerPlan plan) =>
        plan.Name + "|" + plan.Program + "|" + string.Join(' ', plan.Arguments) + "|" + plan.NextUtc?.ToString("O") + "|" + plan.Safety + "|" + plan.WakeToRun;

    public Task<TriggerResult> ApplyAsync(TriggerPlan plan, CancellationToken cancellationToken)
    {
        lock (Applied)
        {
            Applied.Add(plan);
            _existing.Add(plan.Name);
        }

        return Task.FromResult(new TriggerResult(TriggerState.On, string.Empty));
    }

    public Task<TriggerResult> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        lock (Applied)
        {
            Removed.Add(name);
            _existing.Remove(name);
        }

        return Task.FromResult(new TriggerResult(TriggerState.Off, string.Empty));
    }

    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
    {
        lock (Applied)
        {
            return Task.FromResult(_existing.Contains(name));
        }
    }
}
