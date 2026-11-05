using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ICredentialStore"/>: secrets in their own table of the encrypted database.</summary>
public sealed class CredentialStore(IDbContextFactory<PublisherDbContext> factory) : ICredentialStore
{
    /// <inheritdoc />
    public async Task<Credentials> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.Credentials.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == accountId, cancellationToken).ConfigureAwait(false);
        return row is null
            ? Credentials.Empty
            : new Credentials(JsonSerializer.Deserialize(row.Json, StorageJsonContext.Default.DictionaryStringString) ?? []);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Guid accountId, Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var json = JsonSerializer.Serialize(new Dictionary<string, string>(credentials.Values, StringComparer.Ordinal), StorageJsonContext.Default.DictionaryStringString);
        await using var db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.Credentials.FirstOrDefaultAsync(c => c.AccountId == accountId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            db.Credentials.Add(new CredentialRow { AccountId = accountId, Json = json });
        }
        else
        {
            row.Json = json;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
