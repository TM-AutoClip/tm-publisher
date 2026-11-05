namespace TMPublisher.Core.Stats;

/// <summary>Public counters of one published item (<c>null</c> where the platform does not give the number).</summary>
public sealed record PostStats(long? Views, long? Likes, long? Reposts, long? Replies);
