using System.Text.Json;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ISettingsStore"/>: one JSON document.</summary>
public sealed class SettingsStore(IConnectionFactory connections) : ISettingsStore
{
    private const string Key = "app";

    /// <inheritdoc />
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection, "SELECT \"Value\" FROM \"settings\" WHERE \"Key\" = $key", null, ("$key", Key));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string value
            ? JsonSerializer.Deserialize(value, StorageJsonContext.Default.AppSettings) ?? new AppSettings()
            : new AppSettings();
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var value = JsonSerializer.Serialize(settings, StorageJsonContext.Default.AppSettings);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "INSERT INTO \"settings\" (\"Key\", \"Value\") VALUES ($key, $value) ON CONFLICT(\"Key\") DO UPDATE SET \"Value\" = excluded.\"Value\"",
            null, ("$key", Key), ("$value", value));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
