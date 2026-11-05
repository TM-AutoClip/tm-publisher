namespace TMPublisher.Core.Quotas;

/// <summary>Local log of publish attempts: the quota estimate and the statistics read it.</summary>
public interface IPublishLog
{
    /// <summary>Adds an entry.</summary>
    Task AddAsync(PublishLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Entries since the time (UTC), newest first; all accounts when <paramref name="accountId"/> is <c>null</c>.</summary>
    Task<IReadOnlyList<PublishLogEntry>> ListAsync(Guid? accountId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}
