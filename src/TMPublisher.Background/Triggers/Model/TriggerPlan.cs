namespace TMPublisher.Background.Triggers;

/// <summary>
/// What the system scheduler should start, and when: the program with <c>--run-due</c> (and the data folder when it is not the default), at
/// <see cref="NextUtc"/>, at sign-in, and every <see cref="TriggerLimits.SafetyInterval"/> after <see cref="NextUtc"/> while
/// <see cref="Safety"/> is set (a run that ended before it set the next start). Nothing runs in between.
/// </summary>
/// <param name="Name">The task / unit name (one per data folder, <see cref="TriggerNames"/>).</param>
/// <param name="Program">The program file.</param>
/// <param name="Arguments">Its arguments.</param>
/// <param name="NextUtc">When the next run is needed; <c>null</c> when nothing is scheduled (only the sign-in start remains).</param>
/// <param name="Safety">Whether the repeating safety start follows <see cref="NextUtc"/>.</param>
/// <param name="WakeToRun">Whether the computer is woken from sleep for it (Windows; off by default).</param>
public sealed record TriggerPlan(string Name, string Program, IReadOnlyList<string> Arguments, DateTimeOffset? NextUtc, bool Safety, bool WakeToRun);
