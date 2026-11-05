using System.Text.Json;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ICredentialStore"/>: secrets in their own table of the encrypted database.</summary>
public sealed class CredentialStore(IConnectionFactory connections) : ICredentialStore
{
    /// <inheritdoc />
    public async Task<Credentials> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection, "SELECT \"Json\" FROM \"account_secrets\" WHERE \"AccountId\" = $id", null, ("$id", Columns.Id(accountId)));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string json
            ? new Credentials(JsonSerializer.Deserialize(json, StorageJsonContext.Default.DictionaryStringString) ?? [])
            : Credentials.Empty;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Guid accountId, Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var json = JsonSerializer.Serialize(new Dictionary<string, string>(credentials.Values, StringComparer.Ordinal), StorageJsonContext.Default.DictionaryStringString);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "INSERT INTO \"account_secrets\" (\"AccountId\", \"Json\") VALUES ($id, $json) ON CONFLICT(\"AccountId\") DO UPDATE SET \"Json\" = excluded.\"Json\"",
            null, ("$id", Columns.Id(accountId)), ("$json", json));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
