using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ISettingsStore"/>: one JSON document.</summary>
public sealed class SettingsStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), ISettingsStore
{
    private const string Key = "app";

    /// <inheritdoc />
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = Open();
        var value = await context.Settings.AsNoTracking().Where(s => s.Key == Key).Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return value is null ? new AppSettings() : JsonSerializer.Deserialize(value, StorageJsonContext.Default.AppSettings) ?? new AppSettings();
    }

    /// <inheritdoc />
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var value = JsonSerializer.Serialize(settings, StorageJsonContext.Default.AppSettings);
        return SaveRowAsync(new SettingRow { Key = Key, Value = value }, cancellationToken);
    }
}
