namespace TMPublisher.Core.Quotas;

/// <summary>One publish attempt of one target.</summary>
public sealed record PublishLogEntry(Guid AccountId, string Platform, Guid PostId, DateTimeOffset AtUtc, bool Success);
