using Microsoft.Data.Sqlite;

namespace TMPublisher.Storage.Database;

/// <summary>Opens short-lived connections to the encrypted database (one per operation; closing one releases the file).</summary>
public interface IConnectionFactory
{
    /// <summary>An open connection with the database key applied and foreign keys on.</summary>
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
