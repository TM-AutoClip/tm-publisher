using Microsoft.Data.Sqlite;

namespace TMPublisher.Storage.Database;

/// <summary>
/// The one initial schema (no migrations before the first public data). The statements are exactly what EF Core's EnsureCreated made in
/// 0.1.0–0.1.2 (same tables, columns, keys and indexes), so databases made by those versions open unchanged. Values: ids are upper-case
/// GUID text, times are UTC ticks (INTEGER), states are numbers, lists are JSON text. The journal is WAL (set when the file is created).
/// </summary>
public static class Schema
{
    private static readonly string[] Statements =
    [
        """
        CREATE TABLE "account_secrets" (
            "AccountId" TEXT NOT NULL CONSTRAINT "PK_account_secrets" PRIMARY KEY,
            "Json" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "accounts" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_accounts" PRIMARY KEY,
            "Platform" TEXT NOT NULL,
            "DisplayName" TEXT NOT NULL,
            "Handle" TEXT NULL,
            "ExternalId" TEXT NOT NULL,
            "State" INTEGER NOT NULL,
            "ReconnectByUtc" INTEGER NULL,
            "ConnectedAtUtc" INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE "groups" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_groups" PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "AccountIds" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "posts" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_posts" PRIMARY KEY,
            "Kind" INTEGER NOT NULL,
            "Title" TEXT NOT NULL,
            "Body" TEXT NOT NULL,
            "Tags" TEXT NOT NULL,
            "Media" TEXT NOT NULL,
            "ScheduledAtUtc" INTEGER NULL,
            "Status" INTEGER NOT NULL,
            "GroupId" TEXT NULL,
            "CreatedAtUtc" INTEGER NOT NULL,
            "UpdatedAtUtc" INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE "publish_log" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_publish_log" PRIMARY KEY AUTOINCREMENT,
            "AccountId" TEXT NOT NULL,
            "Platform" TEXT NOT NULL,
            "PostId" TEXT NOT NULL,
            "AtUtc" INTEGER NOT NULL,
            "Success" INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE "settings" (
            "Key" TEXT NOT NULL CONSTRAINT "PK_settings" PRIMARY KEY,
            "Value" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "tag_sets" (
            "Platform" TEXT NOT NULL CONSTRAINT "PK_tag_sets" PRIMARY KEY,
            "Tags" TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE "post_targets" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_post_targets" PRIMARY KEY,
            "PostId" TEXT NOT NULL,
            "AccountId" TEXT NOT NULL,
            "Order" INTEGER NOT NULL,
            "Text" TEXT NOT NULL,
            "Title" TEXT NOT NULL,
            "Keywords" TEXT NOT NULL,
            "Options" TEXT NOT NULL,
            "Status" INTEGER NOT NULL,
            "ExternalId" TEXT NULL,
            "Url" TEXT NULL,
            "Message" TEXT NULL,
            "PublishedAtUtc" INTEGER NULL,
            CONSTRAINT "FK_post_targets_posts_PostId" FOREIGN KEY ("PostId") REFERENCES "posts" ("Id") ON DELETE CASCADE
        )
        """,
        """CREATE INDEX "IX_accounts_Platform" ON "accounts" ("Platform")""",
        """CREATE INDEX "IX_post_targets_AccountId" ON "post_targets" ("AccountId")""",
        """CREATE INDEX "IX_post_targets_PostId" ON "post_targets" ("PostId")""",
        """CREATE INDEX "IX_posts_Status_ScheduledAtUtc" ON "posts" ("Status", "ScheduledAtUtc")""",
        """CREATE INDEX "IX_publish_log_AccountId_AtUtc" ON "publish_log" ("AccountId", "AtUtc")""",
    ];

    /// <summary>
    /// Makes the schema in a database without tables (a new file) in one transaction and switches the file to WAL, as EnsureCreated did. A
    /// database that already has tables is not changed. Returns whether the schema was made.
    /// </summary>
    public static async Task<bool> EnsureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using (var count = Columns.Command(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"))
        {
            if ((long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L) > 0)
            {
                return false;
            }
        }

        await using (var journal = Columns.Command(connection, "PRAGMA journal_mode = 'wal'"))
        {
            await journal.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = connection.BeginTransaction(deferred: false);
        foreach (var statement in Statements)
        {
            await using var command = Columns.Command(connection, statement, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Reads the settings table: fails (SqliteException) when the key is wrong or the file is not this database.</summary>
    public static async Task VerifyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = Columns.Command(connection, """SELECT count(*) FROM "settings" """);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
