using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Storage.Database;

namespace TMPublisher.Tests.Storage;

/// <summary>
/// Reads the database as it is stored, without the app's model: every table with its columns as plain text / integer values (no converters),
/// and the schema statements from <c>sqlite_master</c>. The format tests compare files with it.
/// </summary>
internal static class RawTables
{
    /// <summary>The stored columns of every table (the 0.1.2 schema): name and stored type.</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, Type Type)[]> Columns = new Dictionary<string, (string, Type)[]>(StringComparer.Ordinal)
    {
        ["account_secrets"] = [("AccountId", typeof(string)), ("Json", typeof(string))],
        ["accounts"] =
        [
            ("Id", typeof(string)), ("Platform", typeof(string)), ("DisplayName", typeof(string)), ("Handle", typeof(string)), ("ExternalId", typeof(string)),
            ("State", typeof(long)), ("ReconnectByUtc", typeof(long?)), ("ConnectedAtUtc", typeof(long)),
        ],
        ["groups"] = [("Id", typeof(string)), ("Name", typeof(string)), ("AccountIds", typeof(string))],
        ["posts"] =
        [
            ("Id", typeof(string)), ("Kind", typeof(long)), ("Title", typeof(string)), ("Body", typeof(string)), ("Tags", typeof(string)), ("Media", typeof(string)),
            ("ScheduledAtUtc", typeof(long?)), ("Status", typeof(long)), ("GroupId", typeof(string)), ("CreatedAtUtc", typeof(long)), ("UpdatedAtUtc", typeof(long)),
        ],
        ["post_targets"] =
        [
            ("Id", typeof(string)), ("PostId", typeof(string)), ("AccountId", typeof(string)), ("Order", typeof(long)), ("Text", typeof(string)), ("Title", typeof(string)),
            ("Keywords", typeof(string)), ("Options", typeof(string)), ("Status", typeof(long)), ("ExternalId", typeof(string)), ("Url", typeof(string)),
            ("Message", typeof(string)), ("PublishedAtUtc", typeof(long?)),
        ],
        ["publish_log"] =
        [
            ("Id", typeof(long)), ("AccountId", typeof(string)), ("Platform", typeof(string)), ("PostId", typeof(string)), ("AtUtc", typeof(long)), ("Success", typeof(long)),
        ],
        ["settings"] = [("Key", typeof(string)), ("Value", typeof(string))],
        ["tag_sets"] = [("Platform", typeof(string)), ("Tags", typeof(string))],
    };

    /// <summary>Every row of every table as "table: column=value | ...", sorted.</summary>
    public static async Task<List<string>> RowsAsync(string file, string passphrase, CancellationToken cancellationToken)
    {
        await using var context = new RawDbContext(file, passphrase);
        var rows = new List<string>();
        foreach (var (table, columns) in Columns)
        {
            foreach (var row in await context.Set<Dictionary<string, object?>>(table).AsNoTracking().ToListAsync(cancellationToken))
            {
                rows.Add(table + ": " + string.Join(" | ", columns.Select(c => c.Name + "=" + Text(row.GetValueOrDefault(c.Name)))));
            }
        }

        return rows.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Every schema statement as "type name: sql" (white space collapsed), sorted.</summary>
    public static async Task<List<string>> SchemaAsync(string file, string passphrase, CancellationToken cancellationToken)
    {
        await using var context = new RawDbContext(file, passphrase);
        var objects = await context.Set<SqliteMasterRow>().AsNoTracking().Where(o => o.Sql != null && !o.Name.StartsWith("sqlite_"))
            .ToListAsync(cancellationToken);
        return objects.Select(o => o.Type + " " + o.Name + ": " + string.Join(' ', o.Sql!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Order(StringComparer.Ordinal).ToList();
    }

    private static string Text(object? value) => value switch
    {
        null => "NULL",
        string text => "'" + text + "'",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>A context with one property-bag entity per table and <c>sqlite_master</c>, all keyless (read only).</summary>
    private sealed class RawDbContext(string file, string passphrase) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlite(PublisherDbContextFactory.ConnectionString(file, passphrase));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var (table, columns) in Columns)
            {
                modelBuilder.SharedTypeEntity<Dictionary<string, object?>>(table, e =>
                {
                    e.ToTable(table);
                    e.HasNoKey();
                    foreach (var (name, type) in columns)
                    {
                        e.IndexerProperty(type, name);
                    }
                });
            }

            modelBuilder.Entity<SqliteMasterRow>(e =>
            {
                e.ToTable("sqlite_master");
                e.HasNoKey();
                e.Property(o => o.Type).HasColumnName("type");
                e.Property(o => o.Name).HasColumnName("name");
                e.Property(o => o.Sql).HasColumnName("sql");
            });
        }
    }

    /// <summary>One row of <c>sqlite_master</c>.</summary>
    private sealed class SqliteMasterRow
    {
        public string Type { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Sql { get; set; }
    }
}
