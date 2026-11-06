namespace TMPublisher.Background.Triggers;

/// <summary>What writing or removing the system scheduler's entry did.</summary>
/// <param name="State">The state it is in now.</param>
/// <param name="Message">The reason of a failure, or the warning of a fallback (English, for the log; empty when none).</param>
public sealed record TriggerResult(TriggerState State, string Message);
