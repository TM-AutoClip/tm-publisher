using Microsoft.EntityFrameworkCore;

namespace TMPublisher.Storage.Database;

/// <summary>
/// The one schema (no migrations: it has not changed since 0.1.0). EF Core makes it from <see cref="PublisherDbContext"/>'s model, the same
/// tables, columns, keys and indexes as every earlier version (a test compares them with a 0.1.2 database and opens a 0.1.5 one), and switches
/// a new file to WAL.
/// </summary>
public static class Schema
{
    /// <summary>
    /// Makes the schema in a new file (or in a file without tables) with EF Core's EnsureCreated; a database that already has tables is not
    /// changed. Returns whether the schema was made.
    /// </summary>
    public static Task<bool> EnsureAsync(PublisherDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>Reads the settings table: fails (SqliteException) when the key is wrong or the file is not this database.</summary>
    public static async Task VerifyAsync(PublisherDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Settings.CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
