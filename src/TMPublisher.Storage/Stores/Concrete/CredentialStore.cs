using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Stores;

/// <summary><see cref="ICredentialStore"/>: secrets in their own table of the encrypted database.</summary>
public sealed class CredentialStore(IDbContextFactory<PublisherDbContext> contexts) : StoreBase(contexts), ICredentialStore
{
    /// <inheritdoc />
    public async Task<Credentials> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var context = Open();
        var json = await context.Credentials.AsNoTracking().Where(c => c.AccountId == accountId).Select(c => c.Json)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return json is null
            ? Credentials.Empty
            : new Credentials(JsonSerializer.Deserialize(json, StorageJsonContext.Default.DictionaryStringString) ?? []);
    }

    /// <inheritdoc />
    public Task SaveAsync(Guid accountId, Credentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var json = JsonSerializer.Serialize(new Dictionary<string, string>(credentials.Values, StringComparer.Ordinal),
            StorageJsonContext.Default.DictionaryStringString);
        return SaveRowAsync(new CredentialRow { AccountId = accountId, Json = json }, cancellationToken);
    }
}
