using Microsoft.Data.Sqlite;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IAccountStore"/> on the encrypted database.</summary>
public sealed class AccountStore(IConnectionFactory connections) : IAccountStore
{
    private const string Select =
        "SELECT \"Id\", \"Platform\", \"DisplayName\", \"Handle\", \"ExternalId\", \"State\", \"ReconnectByUtc\", \"ConnectedAtUtc\" FROM \"accounts\"";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var accounts = await ReadAsync(connection, Select, cancellationToken).ConfigureAwait(false);
        return accounts.OrderBy(a => a.Platform, StringComparer.Ordinal).ThenBy(a => a.DisplayName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public async Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (await ReadAsync(connection, Select + " WHERE \"Id\" = $id", cancellationToken, ("$id", Columns.Id(id))).ConfigureAwait(false)).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task SaveAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "INSERT INTO \"accounts\" (\"Id\", \"Platform\", \"DisplayName\", \"Handle\", \"ExternalId\", \"State\", \"ReconnectByUtc\", \"ConnectedAtUtc\") "
            + "VALUES ($id, $platform, $name, $handle, $external, $state, $reconnect, $connected) ON CONFLICT(\"Id\") DO UPDATE SET "
            + "\"Platform\" = excluded.\"Platform\", \"DisplayName\" = excluded.\"DisplayName\", \"Handle\" = excluded.\"Handle\", "
            + "\"ExternalId\" = excluded.\"ExternalId\", \"State\" = excluded.\"State\", \"ReconnectByUtc\" = excluded.\"ReconnectByUtc\", "
            + "\"ConnectedAtUtc\" = excluded.\"ConnectedAtUtc\"",
            null, ("$id", Columns.Id(account.Id)), ("$platform", account.Platform), ("$name", account.DisplayName), ("$handle", Columns.OptionalText(account.Handle)),
            ("$external", account.ExternalId), ("$state", (int)account.State), ("$reconnect", Columns.OptionalTicks(account.ReconnectByUtc)),
            ("$connected", account.ConnectedAtUtc.UtcTicks));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetStateAsync(Guid id, AccountState state, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection, "UPDATE \"accounts\" SET \"State\" = $state WHERE \"Id\" = $id", null,
            ("$state", (int)state), ("$id", Columns.Id(id)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        foreach (var sql in new[] { "DELETE FROM \"accounts\" WHERE \"Id\" = $id", "DELETE FROM \"account_secrets\" WHERE \"AccountId\" = $id" })
        {
            await using var command = Columns.Command(connection, sql, transaction, ("$id", Columns.Id(id)));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var group in await GroupStore.ReadAsync(connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            if (group.AccountIds.Remove(id))
            {
                await GroupStore.WriteAsync(connection, transaction, group, cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<Account>> ReadAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var accounts = new List<Account>();
        await using var command = Columns.Command(connection, sql, null, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(new Account
            {
                Id = Columns.ReadId(reader, 0),
                Platform = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Handle = Columns.ReadOptionalText(reader, 3),
                ExternalId = reader.GetString(4),
                State = (AccountState)reader.GetInt32(5),
                ReconnectByUtc = Columns.ReadOptionalTime(reader, 6),
                ConnectedAtUtc = Columns.ReadTime(reader, 7),
            });
        }

        return accounts;
    }
}
