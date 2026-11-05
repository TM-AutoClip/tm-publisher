using Microsoft.Data.Sqlite;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IGroupStore"/> on the encrypted database.</summary>
public sealed class GroupStore(IConnectionFactory connections) : IGroupStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (await ReadAsync(connection, null, cancellationToken).ConfigureAwait(false)).OrderBy(g => g.Name, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public async Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await WriteAsync(connection, null, group, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection, "DELETE FROM \"groups\" WHERE \"Id\" = $id", null, ("$id", Columns.Id(id)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>All groups (also used inside the account delete transaction).</summary>
    internal static async Task<List<Group>> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        var groups = new List<Group>();
        await using var command = Columns.Command(connection, "SELECT \"Id\", \"Name\", \"AccountIds\" FROM \"groups\"", transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            groups.Add(new Group
            {
                Id = Columns.ReadId(reader, 0),
                Name = reader.GetString(1),
                AccountIds = Columns.ReadJson(reader, 2, StorageJsonContext.Default.ListGuid),
            });
        }

        return groups;
    }

    /// <summary>Inserts or replaces one group.</summary>
    internal static async Task WriteAsync(SqliteConnection connection, SqliteTransaction? transaction, Group group, CancellationToken cancellationToken)
    {
        await using var command = Columns.Command(connection,
            "INSERT INTO \"groups\" (\"Id\", \"Name\", \"AccountIds\") VALUES ($id, $name, $accounts) "
            + "ON CONFLICT(\"Id\") DO UPDATE SET \"Name\" = excluded.\"Name\", \"AccountIds\" = excluded.\"AccountIds\"",
            transaction, ("$id", Columns.Id(group.Id)), ("$name", group.Name), ("$accounts", Columns.Json(group.AccountIds, StorageJsonContext.Default.ListGuid)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
