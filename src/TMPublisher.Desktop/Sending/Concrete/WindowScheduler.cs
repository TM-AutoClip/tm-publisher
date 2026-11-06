using TMPublisher.Core.Posts;
using TMPublisher.Scheduling;

namespace TMPublisher.Desktop.Sending;

/// <summary>
/// <see cref="IPostScheduler"/> of the window: the user's decisions are stored here (<see cref="PostDecisions"/>, conditional on the post's
/// state), then the schedule is read again (<see cref="WindowSender"/>), which also sets the system scheduler's next start.
/// </summary>
public sealed class WindowScheduler : IPostScheduler
{
    private readonly PostDecisions _decisions;
    private readonly WindowSender _sender;

    /// <summary>Creates.</summary>
    public WindowScheduler(PostDecisions decisions, WindowSender sender, PostScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _decisions = decisions;
        _sender = sender;
        scheduler.AttentionChanged += (_, _) => AttentionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? AttentionChanged;

    /// <inheritdoc />
    public void Wake() => _sender.Wake();

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _sender.RestartAsync().ConfigureAwait(false);
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
            _sender.Wake();
        }

        AttentionChanged?.Invoke(this, EventArgs.Empty);
        return applied;
    }
}
