using System.Text.RegularExpressions;
using Shouldly;
using TMPublisher.Storage.Database;

namespace TMPublisher.Tests.Storage;

/// <summary>
/// The hand-written schema is the one EF Core's EnsureCreated made in 0.1.0–0.1.2 (captured from a 0.1.2 database), so their databases open
/// unchanged; values are stored in the same formats.
/// </summary>
public sealed partial class SchemaTests
{
    private static readonly string[] Stored012 =
    [
        "table account_secrets: CREATE TABLE \"account_secrets\" ( \"AccountId\" TEXT NOT NULL CONSTRAINT \"PK_account_secrets\" PRIMARY KEY, \"Json\" TEXT NOT NULL )",
        "table accounts: CREATE TABLE \"accounts\" ( \"Id\" TEXT NOT NULL CONSTRAINT \"PK_accounts\" PRIMARY KEY, \"Platform\" TEXT NOT NULL, \"DisplayName\" TEXT NOT NULL, \"Handle\" TEXT NULL, \"ExternalId\" TEXT NOT NULL, \"State\" INTEGER NOT NULL, \"ReconnectByUtc\" INTEGER NULL, \"ConnectedAtUtc\" INTEGER NOT NULL )",
        "table groups: CREATE TABLE \"groups\" ( \"Id\" TEXT NOT NULL CONSTRAINT \"PK_groups\" PRIMARY KEY, \"Name\" TEXT NOT NULL, \"AccountIds\" TEXT NOT NULL )",
        "table posts: CREATE TABLE \"posts\" ( \"Id\" TEXT NOT NULL CONSTRAINT \"PK_posts\" PRIMARY KEY, \"Kind\" INTEGER NOT NULL, \"Title\" TEXT NOT NULL, \"Body\" TEXT NOT NULL, \"Tags\" TEXT NOT NULL, \"Media\" TEXT NOT NULL, \"ScheduledAtUtc\" INTEGER NULL, \"Status\" INTEGER NOT NULL, \"GroupId\" TEXT NULL, \"CreatedAtUtc\" INTEGER NOT NULL, \"UpdatedAtUtc\" INTEGER NOT NULL )",
        "table publish_log: CREATE TABLE \"publish_log\" ( \"Id\" INTEGER NOT NULL CONSTRAINT \"PK_publish_log\" PRIMARY KEY AUTOINCREMENT, \"AccountId\" TEXT NOT NULL, \"Platform\" TEXT NOT NULL, \"PostId\" TEXT NOT NULL, \"AtUtc\" INTEGER NOT NULL, \"Success\" INTEGER NOT NULL )",
        "table settings: CREATE TABLE \"settings\" ( \"Key\" TEXT NOT NULL CONSTRAINT \"PK_settings\" PRIMARY KEY, \"Value\" TEXT NOT NULL )",
        "table tag_sets: CREATE TABLE \"tag_sets\" ( \"Platform\" TEXT NOT NULL CONSTRAINT \"PK_tag_sets\" PRIMARY KEY, \"Tags\" TEXT NOT NULL )",
        "table post_targets: CREATE TABLE \"post_targets\" ( \"Id\" TEXT NOT NULL CONSTRAINT \"PK_post_targets\" PRIMARY KEY, \"PostId\" TEXT NOT NULL, \"AccountId\" TEXT NOT NULL, \"Order\" INTEGER NOT NULL, \"Text\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, \"Keywords\" TEXT NOT NULL, \"Options\" TEXT NOT NULL, \"Status\" INTEGER NOT NULL, \"ExternalId\" TEXT NULL, \"Url\" TEXT NULL, \"Message\" TEXT NULL, \"PublishedAtUtc\" INTEGER NULL, CONSTRAINT \"FK_post_targets_posts_PostId\" FOREIGN KEY (\"PostId\") REFERENCES \"posts\" (\"Id\") ON DELETE CASCADE )",
        "index IX_accounts_Platform: CREATE INDEX \"IX_accounts_Platform\" ON \"accounts\" (\"Platform\")",
        "index IX_post_targets_AccountId: CREATE INDEX \"IX_post_targets_AccountId\" ON \"post_targets\" (\"AccountId\")",
        "index IX_post_targets_PostId: CREATE INDEX \"IX_post_targets_PostId\" ON \"post_targets\" (\"PostId\")",
        "index IX_posts_Status_ScheduledAtUtc: CREATE INDEX \"IX_posts_Status_ScheduledAtUtc\" ON \"posts\" (\"Status\", \"ScheduledAtUtc\")",
        "index IX_publish_log_AccountId_AtUtc: CREATE INDEX \"IX_publish_log_AccountId_AtUtc\" ON \"publish_log\" (\"AccountId\", \"AtUtc\")",
    ];

    [Fact]
    public async Task TheSchemaIsTheOneOfVersion012()
    {
        using var h = await StorageHarness.CreatedAsync();
        await using var connection = await h.Factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name, sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'";
        var made = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                made.Add(reader.GetString(0) + " " + reader.GetString(1) + ": " + Spaces().Replace(reader.GetString(2), " "));
            }
        }

        made.Order(StringComparer.Ordinal).ShouldBe(Stored012.Order(StringComparer.Ordinal));
        await using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode";
        (await journal.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldBe("wal");
    }

    [Fact]
    public async Task ValuesAreStoredInTheFormatsOfVersion012()
    {
        using var h = await StorageHarness.CreatedAsync();
        var id = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 11, 5, 12, 30, 0, TimeSpan.FromHours(3));
        await new TMPublisher.Storage.Stores.AccountStore(h.Factory).SaveAsync(
            new TMPublisher.Core.Accounts.Account { Id = id, Platform = "x", DisplayName = "n", ConnectedAtUtc = at }, CancellationToken.None);
        await using var connection = await h.Factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Id\", \"ConnectedAtUtc\", typeof(\"ConnectedAtUtc\"), \"ReconnectByUtc\" IS NULL FROM \"accounts\"";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(id.ToString("D").ToUpperInvariant());
        reader.GetInt64(1).ShouldBe(at.UtcTicks);
        reader.GetString(2).ShouldBe("integer");
        reader.GetInt64(3).ShouldBe(1);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
