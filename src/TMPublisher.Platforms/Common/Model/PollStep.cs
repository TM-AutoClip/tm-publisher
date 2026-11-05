namespace TMPublisher.Platforms.Common;

/// <summary>One round of polling: done (with its value) or wait and ask again.</summary>
public readonly record struct PollStep<T>(bool IsDone, T? Value, TimeSpan Wait)
{
    /// <summary>Done.</summary>
    public static PollStep<T> Done(T value) => new(true, value, TimeSpan.Zero);

    /// <summary>Ask again after <paramref name="wait"/>.</summary>
    public static PollStep<T> Again(TimeSpan wait) => new(false, default, wait);
}
