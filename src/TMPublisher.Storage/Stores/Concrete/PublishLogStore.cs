using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Quotas;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPublishLog"/> on the encrypted database.</summary>
public sealed class PublishLogStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), IPublishLog
{
    /// <inheritdoc />
    public async Task AddAsync(PublishLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var context = Open();
        context.PublishLog.Add(new PublishLogRow
        {
            AccountId = entry.AccountId,
            Platform = entry.Platform,
            PostId = entry.PostId,
            AtUtc = entry.AtUtc,
            Success = entry.Success,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishLogEntry>> ListAsync(Guid? accountId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = Open();
        var rows = context.PublishLog.AsNoTracking().Where(l => l.AtUtc >= sinceUtc);
        if (accountId is { } id)
        {
            rows = rows.Where(l => l.AccountId == id);
        }

        var entries = await rows.OrderByDescending(l => l.AtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
        return entries.Select(l => new PublishLogEntry(l.AccountId, l.Platform, l.PostId, l.AtUtc, l.Success)).ToList();
    }
}
