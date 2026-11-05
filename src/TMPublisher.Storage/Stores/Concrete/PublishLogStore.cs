using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Quotas;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPublishLog"/> on the encrypted database.</summary>
public sealed class PublishLogStore(IDbContextFactory<PublisherDbContext> factory) : IPublishLog
{
    /// <inheritdoc />
    public async Task AddAsync(PublishLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.PublishLog.Add(new PublishLogRow
        {
            AccountId = entry.AccountId,
            Platform = entry.Platform,
            PostId = entry.PostId,
            AtUtc = entry.AtUtc,
            Success = entry.Success,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishLogEntry>> ListAsync(Guid? accountId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = db.PublishLog.AsNoTracking().Where(l => l.AtUtc >= sinceUtc);
        if (accountId is { } id)
        {
            query = query.Where(l => l.AccountId == id);
        }

        var rows = await query.OrderByDescending(l => l.AtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(r => new PublishLogEntry(r.AccountId, r.Platform, r.PostId, r.AtUtc, r.Success)).ToList();
    }
}
