using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary>
/// What every store does with the database: a short-lived context per operation, a write transaction that takes SQLite's write lock at once,
/// and "insert or replace" of one row.
/// </summary>
public abstract class StoreBase(IDbContextFactory<PublisherDbContext> contexts)
{
    /// <summary>A new context (one per operation).</summary>
    protected PublisherDbContext Open() => contexts.CreateDbContext();

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction and commits it. BEGIN IMMEDIATE takes SQLite's write lock before anything is read, so
    /// the other process (the window or a run of the system scheduler) cannot change a row between a check and the write that depends on it.
    /// </summary>
    protected async Task<T> InWriteTransactionAsync<T>(Func<PublisherDbContext, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var context = Open();
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: false);
        await context.Database.UseTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        var result = await work(context).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Runs <paramref name="work"/> in one write transaction (<see cref="InWriteTransactionAsync{T}"/>).</summary>
    protected Task InWriteTransactionAsync(Func<PublisherDbContext, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InWriteTransactionAsync(async context =>
        {
            await work(context).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Saves one row in its own write transaction: inserted when its key is not stored yet, replaced otherwise.</summary>
    protected Task SaveRowAsync<TRow>(TRow row, CancellationToken cancellationToken)
        where TRow : class =>
        InWriteTransactionAsync(async context =>
        {
            await TrackAsync(context, row, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>
    /// Tracks <paramref name="row"/> as added when its key is not stored yet, as modified (every column written) otherwise. Only this row is
    /// tracked, not the rows it refers to. Called inside a write transaction.
    /// </summary>
    protected static async Task TrackAsync<TRow>(PublisherDbContext context, TRow row, CancellationToken cancellationToken)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);
        var entry = context.Entry(row);
        var stored = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
        entry.State = stored is null ? EntityState.Added : EntityState.Modified;
    }
}
