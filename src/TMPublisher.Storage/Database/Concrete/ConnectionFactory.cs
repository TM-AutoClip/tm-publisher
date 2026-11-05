using Microsoft.Data.Sqlite;
using TMPublisher.Core.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Storage.Database;

/// <summary>
/// Connections to the encrypted file. The key goes in the connection string's Password (Microsoft.Data.Sqlite sends it as <c>PRAGMA key</c>,
/// SQLite3 Multiple Ciphers encrypts with its default ChaCha20-Poly1305). Pooling is off so a closed connection releases the file (a restore
/// replaces it, and the background scheduler and the window open the same file). While the other process writes, SQLite answers "busy" and
/// Microsoft.Data.Sqlite tries again until the command timeout (30 s).
/// </summary>
public sealed class ConnectionFactory(IAppPaths paths, DatabaseKey key) : IConnectionFactory
{
    /// <inheritdoc />
    public Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken) => OpenFileAsync(paths.DatabaseFile, key.Passphrase(), cancellationToken);

    /// <summary>Connection string of an encrypted file.</summary>
    public static string ConnectionString(string file, string passphrase) => new SqliteConnectionStringBuilder
    {
        DataSource = file,
        Password = passphrase,
        Pooling = false,
        ForeignKeys = true,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    /// <summary>Opens an encrypted file (the live database or a backup copy).</summary>
    public static async Task<SqliteConnection> OpenFileAsync(string file, string passphrase, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString(file, passphrase));
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
