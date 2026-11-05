namespace TMPublisher.Runtime.Ipc;

/// <summary>The answer line of the background scheduler, or why it could not be reached.</summary>
/// <param name="Reached">Whether the scheduler answered.</param>
/// <param name="Text">The answer line; when not reached, the reason (exception type and message, no secrets).</param>
public sealed record SchedulerReply(bool Reached, string Text)
{
    /// <summary>Whether it answered <see cref="SchedulerCommands.Ok"/>.</summary>
    public bool IsOk => Reached && Text == SchedulerCommands.Ok;
}
