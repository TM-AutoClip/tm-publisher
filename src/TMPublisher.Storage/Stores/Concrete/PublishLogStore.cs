using TMPublisher.Core.Quotas;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPublishLog"/> on the encrypted database.</summary>
public sealed class PublishLogStore(IConnectionFactory connections) : IPublishLog
{
    /// <inheritdoc />
    public async Task AddAsync(PublishLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "INSERT INTO \"publish_log\" (\"AccountId\", \"Platform\", \"PostId\", \"AtUtc\", \"Success\") VALUES ($account, $platform, $post, $at, $success)",
            null, ("$account", Columns.Id(entry.AccountId)), ("$platform", entry.Platform), ("$post", Columns.Id(entry.PostId)), ("$at", entry.AtUtc.UtcTicks),
            ("$success", entry.Success ? 1 : 0));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishLogEntry>> ListAsync(Guid? accountId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = "SELECT \"AccountId\", \"Platform\", \"PostId\", \"AtUtc\", \"Success\" FROM \"publish_log\" WHERE \"AtUtc\" >= $since"
            + (accountId is null ? string.Empty : " AND \"AccountId\" = $account") + " ORDER BY \"AtUtc\" DESC";
        await using var command = accountId is { } id
            ? Columns.Command(connection, sql, null, ("$since", sinceUtc.UtcTicks), ("$account", Columns.Id(id)))
            : Columns.Command(connection, sql, null, ("$since", sinceUtc.UtcTicks));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<PublishLogEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new PublishLogEntry(Columns.ReadId(reader, 0), reader.GetString(1), Columns.ReadId(reader, 2), Columns.ReadTime(reader, 3), reader.GetInt64(4) != 0));
        }

        return entries;
    }
}
