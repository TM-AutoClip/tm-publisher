using System.Globalization;
using Microsoft.Data.Sqlite;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="IPostStore"/> on the encrypted database (plain SQL; the window and the background scheduler share the file).</summary>
public sealed class PostStore(IConnectionFactory connections, TimeProvider clock) : IPostStore
{
    private const string PostColumns =
        "\"Id\", \"Kind\", \"Title\", \"Body\", \"Tags\", \"Media\", \"ScheduledAtUtc\", \"Status\", \"GroupId\", \"CreatedAtUtc\", \"UpdatedAtUtc\"";

    private const string TargetColumns =
        "\"Id\", \"PostId\", \"AccountId\", \"Order\", \"Text\", \"Title\", \"Keywords\", \"Options\", \"Status\", \"ExternalId\", \"Url\", \"Message\", \"PublishedAtUtc\"";

    private static StorageJsonContext Json => StorageJsonContext.Default;

    /// <inheritdoc />
    public async Task<Post?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (await ReadAsync(connection, null, "\"Id\" = $id", null, cancellationToken, ("$id", Columns.Id(id))).ConfigureAwait(false)).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var posts = await ReadAsync(connection, null,
            "(\"ScheduledAtUtc\" IS NOT NULL AND \"ScheduledAtUtc\" >= $from AND \"ScheduledAtUtc\" < $to) "
            + "OR (\"ScheduledAtUtc\" IS NULL AND \"CreatedAtUtc\" >= $from AND \"CreatedAtUtc\" < $to)",
            null, cancellationToken, ("$from", fromUtc.UtcTicks), ("$to", toUtc.UtcTicks)).ConfigureAwait(false);
        return posts.OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListByStatusAsync(IReadOnlyCollection<PostStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        if (statuses.Count == 0)
        {
            return [];
        }

        // The states are numbers of our own enum, written into the statement as numbers (nothing from the user).
        var list = string.Join(", ", statuses.Select(s => ((int)s).ToString(CultureInfo.InvariantCulture)));
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var posts = await ReadAsync(connection, null, $"\"Status\" IN ({list})", null, cancellationToken).ConfigureAwait(false);
        return posts.OrderBy(p => p.ScheduledAtUtc ?? p.CreatedAtUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<Post?> NextScheduledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var posts = await ReadAsync(connection, null, "\"Status\" = $status AND \"ScheduledAtUtc\" IS NOT NULL", "ORDER BY \"ScheduledAtUtc\" LIMIT 1",
            cancellationToken, ("$status", (int)PostStatus.Scheduled)).ConfigureAwait(false);
        return posts.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task SaveAsync(Post post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        var now = clock.GetUtcNow();
        if (post.CreatedAtUtc == default)
        {
            post.CreatedAtUtc = now;
        }

        post.UpdatedAtUtc = now;
        foreach (var target in post.Targets)
        {
            target.PostId = post.Id;
        }

        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await WriteAsync(connection, transaction, post, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> SaveEditAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        SaveIfAsync(post, expectedStates, keepFinished: true, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SaveDecisionAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, CancellationToken cancellationToken) =>
        SaveIfAsync(post, expectedStates, keepFinished: false, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryMoveAsync(PostClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "UPDATE \"posts\" SET \"Status\" = $to, \"UpdatedAtUtc\" = $now WHERE \"Id\" = $id AND \"Status\" = $from AND \"ScheduledAtUtc\" IS $at",
            null, ("$to", (int)claim.To), ("$now", clock.GetUtcNow().UtcTicks), ("$id", Columns.Id(claim.PostId)), ("$from", (int)claim.From),
            ("$at", Columns.OptionalTicks(claim.ScheduledAtUtc)));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public async Task SaveTargetAsync(PostTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Columns.Command(connection,
            "UPDATE \"post_targets\" SET \"PostId\" = $post, \"AccountId\" = $account, \"Order\" = $order, \"Text\" = $text, \"Title\" = $title, "
            + "\"Keywords\" = $keywords, \"Options\" = $options, \"Status\" = $status, \"ExternalId\" = $external, \"Url\" = $url, \"Message\" = $message, "
            + "\"PublishedAtUtc\" = $published WHERE \"Id\" = $id",
            null, TargetParameters(target));
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException($"Target {target.Id} of post {target.PostId} is no longer stored; its state was not saved.");
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using (var targets = Columns.Command(connection, "DELETE FROM \"post_targets\" WHERE \"PostId\" = $id", transaction, ("$id", Columns.Id(id))))
        {
            await targets.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var post = Columns.Command(connection, "DELETE FROM \"posts\" WHERE \"Id\" = $id", transaction, ("$id", Columns.Id(id))))
        {
            await post.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the post only while the stored one is in <paramref name="expectedStates"/>. BEGIN IMMEDIATE takes SQLite's write lock at once, so
    /// the scheduler (also in the other process) cannot change the post between this check and the write. An edit keeps the stored targets that
    /// already reached their platform (<see cref="PostEditing.KeepFinished"/>); a decision is stored as given.
    /// </summary>
    private async Task<bool> SaveIfAsync(Post post, IReadOnlyCollection<PostStatus> expectedStates, bool keepFinished, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(expectedStates);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var stored = (await ReadAsync(connection, transaction, "\"Id\" = $id", null, cancellationToken, ("$id", Columns.Id(post.Id))).ConfigureAwait(false))
            .FirstOrDefault();
        if (stored is not null)
        {
            if (!expectedStates.Contains(stored.Status))
            {
                return false;
            }

            if (keepFinished)
            {
                post.Targets = PostEditing.KeepFinished(post.Targets, stored.Targets);
            }

            post.CreatedAtUtc = stored.CreatedAtUtc;
        }

        post.UpdatedAtUtc = clock.GetUtcNow();
        if (post.CreatedAtUtc == default)
        {
            post.CreatedAtUtc = post.UpdatedAtUtc;
        }

        foreach (var target in post.Targets)
        {
            target.PostId = post.Id;
        }

        await WriteAsync(connection, transaction, post, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Posts matching <paramref name="where"/> with their targets in sending order (two statements, same condition).</summary>
    private static async Task<List<Post>> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, string where, string? tail,
        CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        var posts = new List<Post>();
        var byId = new Dictionary<Guid, Post>();
        await using (var command = Columns.Command(connection, $"SELECT {PostColumns} FROM \"posts\" WHERE {where} {tail}", transaction, parameters))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var post = new Post
                {
                    Id = Columns.ReadId(reader, 0),
                    Kind = (PostKind)reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Body = reader.GetString(3),
                    Tags = Columns.ReadJson(reader, 4, Json.ListString),
                    Media = Columns.ReadJson(reader, 5, Json.ListMediaItem),
                    ScheduledAtUtc = Columns.ReadOptionalTime(reader, 6),
                    Status = (PostStatus)reader.GetInt32(7),
                    GroupId = Columns.ReadOptionalId(reader, 8),
                    CreatedAtUtc = Columns.ReadTime(reader, 9),
                    UpdatedAtUtc = Columns.ReadTime(reader, 10),
                };
                posts.Add(post);
                byId[post.Id] = post;
            }
        }

        if (posts.Count == 0)
        {
            return posts;
        }

        var sql = $"SELECT {TargetColumns} FROM \"post_targets\" WHERE \"PostId\" IN (SELECT \"Id\" FROM \"posts\" WHERE {where} {tail}) ORDER BY \"Order\"";
        await using (var command = Columns.Command(connection, sql, transaction, parameters))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var target = new PostTarget
                {
                    Id = Columns.ReadId(reader, 0),
                    PostId = Columns.ReadId(reader, 1),
                    AccountId = Columns.ReadId(reader, 2),
                    Order = reader.GetInt32(3),
                    Text = reader.GetString(4),
                    Title = reader.GetString(5),
                    Keywords = Columns.ReadJson(reader, 6, Json.ListString),
                    Options = Columns.ReadJson(reader, 7, Json.DictionaryStringString),
                    Status = (TargetStatus)reader.GetInt32(8),
                    ExternalId = Columns.ReadOptionalText(reader, 9),
                    Url = Columns.ReadOptionalText(reader, 10),
                    Message = Columns.ReadOptionalText(reader, 11),
                    PublishedAtUtc = Columns.ReadOptionalTime(reader, 12),
                };
                if (byId.TryGetValue(target.PostId, out var owner))
                {
                    owner.Targets.Add(target);
                }
            }
        }

        return posts;
    }

    /// <summary>Replaces the post row (upsert, so the targets are not cascaded away) and all its targets.</summary>
    private static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction, Post post, CancellationToken cancellationToken)
    {
        await using (var delete = Columns.Command(connection, "DELETE FROM \"post_targets\" WHERE \"PostId\" = $id", transaction, ("$id", Columns.Id(post.Id))))
        {
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var upsert = Columns.Command(connection,
            $"INSERT INTO \"posts\" ({PostColumns}) VALUES ($id, $kind, $title, $body, $tags, $media, $at, $status, $group, $created, $updated) "
            + "ON CONFLICT(\"Id\") DO UPDATE SET \"Kind\" = excluded.\"Kind\", \"Title\" = excluded.\"Title\", \"Body\" = excluded.\"Body\", "
            + "\"Tags\" = excluded.\"Tags\", \"Media\" = excluded.\"Media\", \"ScheduledAtUtc\" = excluded.\"ScheduledAtUtc\", \"Status\" = excluded.\"Status\", "
            + "\"GroupId\" = excluded.\"GroupId\", \"CreatedAtUtc\" = excluded.\"CreatedAtUtc\", \"UpdatedAtUtc\" = excluded.\"UpdatedAtUtc\"",
            transaction,
            ("$id", Columns.Id(post.Id)), ("$kind", (int)post.Kind), ("$title", post.Title), ("$body", post.Body), ("$tags", Columns.Json(post.Tags, Json.ListString)),
            ("$media", Columns.Json(post.Media, Json.ListMediaItem)), ("$at", Columns.OptionalTicks(post.ScheduledAtUtc)), ("$status", (int)post.Status),
            ("$group", Columns.OptionalId(post.GroupId)), ("$created", post.CreatedAtUtc.UtcTicks), ("$updated", post.UpdatedAtUtc.UtcTicks)))
        {
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var target in post.Targets)
        {
            await using var insert = Columns.Command(connection,
                $"INSERT INTO \"post_targets\" ({TargetColumns}) VALUES ($id, $post, $account, $order, $text, $title, $keywords, $options, $status, $external, $url, "
                + "$message, $published)",
                transaction, TargetParameters(target));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static (string Name, object Value)[] TargetParameters(PostTarget target) =>
    [
        ("$id", Columns.Id(target.Id)), ("$post", Columns.Id(target.PostId)), ("$account", Columns.Id(target.AccountId)), ("$order", target.Order),
        ("$text", target.Text), ("$title", target.Title), ("$keywords", Columns.Json(target.Keywords, Json.ListString)),
        ("$options", Columns.Json(target.Options, Json.DictionaryStringString)), ("$status", (int)target.Status), ("$external", Columns.OptionalText(target.ExternalId)),
        ("$url", Columns.OptionalText(target.Url)), ("$message", Columns.OptionalText(target.Message)), ("$published", Columns.OptionalTicks(target.PublishedAtUtc)),
    ];
}
