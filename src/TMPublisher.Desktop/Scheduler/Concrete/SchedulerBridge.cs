using TMPublisher.Core.Posts;
using TMPublisher.Scheduling;

namespace TMPublisher.Desktop.Scheduler;

/// <summary>
/// <see cref="IPostScheduler"/> of the window: the user's decisions are stored here (<see cref="PostDecisions"/>, conditional on the post's
/// state), and the background scheduler is told to read the schedule again; it is the only one that sends.
/// </summary>
public sealed class SchedulerBridge : IPostScheduler
{
    private readonly PostDecisions _decisions;
    private readonly SchedulerSupervisor _supervisor;

    /// <summary>Creates.</summary>
    public SchedulerBridge(PostDecisions decisions, SchedulerSupervisor supervisor)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        _decisions = decisions;
        _supervisor = supervisor;
        supervisor.PostsChanged += (_, _) => AttentionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? AttentionChanged;

    /// <inheritdoc />
    public void Wake() => _supervisor.Wake();

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _supervisor.EnsureRunningAsync(cancellationToken).ConfigureAwait(false);
        AttentionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<bool> SendNowAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(_decisions.SendNowAsync(postId, cancellationToken));

    /// <inheritdoc />
    public Task<bool> RescheduleAsync(Guid postId, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
        DecideAsync(_decisions.RescheduleAsync(postId, atUtc, cancellationToken));

    /// <inheritdoc />
    public Task<bool> CancelAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(_decisions.CancelAsync(postId, cancellationToken));

    /// <inheritdoc />
    public Task<bool> RetryAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(_decisions.RetryAsync(postId, cancellationToken));

    /// <inheritdoc />
    public Task<bool> SkipFailedAsync(Guid postId, CancellationToken cancellationToken) => DecideAsync(_decisions.SkipFailedAsync(postId, cancellationToken));

    private async Task<bool> DecideAsync(Task<bool> decision)
    {
        var applied = await decision.ConfigureAwait(false);
        if (applied)
        {
            _supervisor.Wake();
        }

        AttentionChanged?.Invoke(this, EventArgs.Empty);
        return applied;
    }
}
