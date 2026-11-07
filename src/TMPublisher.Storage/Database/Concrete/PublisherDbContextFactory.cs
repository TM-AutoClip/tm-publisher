using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Storage.Database;

/// <summary>
/// Short-lived contexts on the encrypted file (one per operation; disposing one closes its connection and releases the file). The key goes in
/// the connection string's Password (Microsoft.Data.Sqlite sends it as <c>PRAGMA key</c>, SQLite3 Multiple Ciphers encrypts with its default
/// ChaCha20-Poly1305). Pooling is off so a closed connection releases the file (a restore replaces it, and a run of the system scheduler and
/// the window open the same file); foreign keys are on, so deleting a post also deletes its targets. While the other process writes, SQLite
/// answers "busy" and Microsoft.Data.Sqlite tries again until the command timeout (30 s).
/// </summary>
public sealed class PublisherDbContextFactory(IAppPaths paths, DatabaseKey key) : IDbContextFactory<PublisherDbContext>
{
    /// <inheritdoc />
    public PublisherDbContext CreateDbContext() => ForFile(paths.DatabaseFile, key.Passphrase());

    /// <summary>Connection string of an encrypted file.</summary>
    public static string ConnectionString(string file, string passphrase) => new SqliteConnectionStringBuilder
    {
        DataSource = file,
        Password = passphrase,
        Pooling = false,
        ForeignKeys = true,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    /// <summary>A context on an encrypted file (the live database or a backup copy).</summary>
    public static PublisherDbContext ForFile(string file, string passphrase) =>
        new(new DbContextOptionsBuilder<PublisherDbContext>().UseSqlite(ConnectionString(file, passphrase)).Options);
}
