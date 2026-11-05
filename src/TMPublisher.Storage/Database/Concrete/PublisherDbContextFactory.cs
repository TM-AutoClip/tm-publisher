using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Storage.Database;

/// <summary>
/// Short-lived contexts on the encrypted file. The key goes in the connection string's Password (Microsoft.Data.Sqlite sends it as
/// <c>PRAGMA key</c>, SQLite3 Multiple Ciphers encrypts with its default ChaCha20-Poly1305). Pooling is off so a closed database releases
/// its file (restore replaces it).
/// </summary>
public sealed class PublisherDbContextFactory(IAppPaths paths, DatabaseKey key) : IDbContextFactory<PublisherDbContext>
{
    /// <inheritdoc />
    public PublisherDbContext CreateDbContext() => new(Options(ConnectionString(paths.DatabaseFile, key.Passphrase())));

    /// <summary>Connection string of an encrypted file.</summary>
    public static string ConnectionString(string file, string passphrase) => new SqliteConnectionStringBuilder
    {
        DataSource = file,
        Password = passphrase,
        Pooling = false,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    /// <summary>Context options for a connection string.</summary>
    public static DbContextOptions<PublisherDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<PublisherDbContext>().UseSqlite(connectionString).Options;
}
