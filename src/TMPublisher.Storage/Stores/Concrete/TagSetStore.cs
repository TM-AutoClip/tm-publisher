using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ITagSetStore"/> on the encrypted database.</summary>
public sealed class TagSetStore(IConnectionFactory connections) : ITagSetStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TagSet>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection, "SELECT \"Platform\", \"Tags\" FROM \"tag_sets\"");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var sets = new List<TagSet>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sets.Add(new TagSet { Platform = reader.GetString(0), Tags = Columns.ReadJson(reader, 1, StorageJsonContext.Default.ListString) });
        }

        return sets;
    }

    /// <inheritdoc />
    public async Task SaveAsync(TagSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "INSERT INTO \"tag_sets\" (\"Platform\", \"Tags\") VALUES ($platform, $tags) ON CONFLICT(\"Platform\") DO UPDATE SET \"Tags\" = excluded.\"Tags\"",
            null, ("$platform", set.Platform), ("$tags", Columns.Json(set.Tags, StorageJsonContext.Default.ListString)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
