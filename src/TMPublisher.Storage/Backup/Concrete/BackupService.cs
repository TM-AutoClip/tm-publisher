using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Storage.Backup;

/// <summary>
/// Backups as one file (<c>.tmpbak</c>): <see cref="SealedBox"/> with the magic word <c>TMPBAK01</c> around
/// [key length (1) | database key (32) | encrypted database copy]. The copy is made with SQLite's online backup into a file encrypted with
/// the same key, so no plain data is written to disk at any moment. Only the database is backed up, not the media files.
/// A forgotten backup password cannot be recovered.
/// </summary>
public sealed class BackupService(IAppPaths paths, DatabaseKey key, Vault vault, KdfParameters kdf, IAccountStore accountStore, IPostStore postStore,
    ILogger<BackupService> logger)
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TMPBAK01");

    /// <summary>File extension of backups.</summary>
    public const string Extension = ".tmpbak";

    /// <summary>Writes a backup of the open database to <paramref name="target"/>.</summary>
    public async Task CreateAsync(string target, string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        if (!BackupPasswordPolicy.IsAcceptable(password))
        {
            throw new ArgumentException($"The backup password must be at least {BackupPasswordPolicy.MinimumLength} characters.", nameof(password));
        }

        var copy = TemporaryFile();
        var dek = key.Copy();
        try
        {
            var passphrase = key.Passphrase();
            using (var source = new SqliteConnection(ConnectionFactory.ConnectionString(paths.DatabaseFile, passphrase)))
            using (var destination = new SqliteConnection(ConnectionFactory.ConnectionString(copy, passphrase)))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            var database = await File.ReadAllBytesAsync(copy, cancellationToken).ConfigureAwait(false);
            var plain = new byte[1 + dek.Length + database.Length];
            plain[0] = (byte)dek.Length;
            dek.CopyTo(plain, 1);
            database.CopyTo(plain, 1 + dek.Length);
            var sealedData = SealedBox.Seal(Magic, password, plain, kdf);
            CryptographicOperations.ZeroMemory(plain);
            var temporary = target + ".tmp";
            await File.WriteAllBytesAsync(temporary, sealedData, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, target, overwrite: true);
            logger.LogInformation("Backup written ({Kilobytes} KB)", sealedData.Length / 1024);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
            Delete(copy);
        }
    }

    /// <summary>
    /// Restores a backup into this computer's data folder. The current database and its key file are kept aside, never deleted, and are
    /// put back when the restore fails (<see cref="Vault.ReplaceAsync"/>). The key is sealed again with <paramref name="protector"/> (this
    /// computer's keyring or a master password).
    /// <see cref="SealedBoxException"/> for a wrong password or a damaged file.
    /// </summary>
    public async Task<RestoreReport> RestoreAsync(string source, string password, IKeyProtector protector, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentNullException.ThrowIfNull(protector);
        var sealedData = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
        var plain = SealedBox.Open(Magic, password, sealedData);
        var copy = TemporaryFile();
        var dek = Array.Empty<byte>();
        try
        {
            if (plain.Length < 33 || plain[0] != 32)
            {
                throw new SealedBoxException("The backup content is damaged.");
            }

            dek = plain.AsSpan(1, 32).ToArray();
            await File.WriteAllBytesAsync(copy, plain.AsMemory(33).ToArray(), cancellationToken).ConfigureAwait(false);
            await VerifyAsync(copy, dek, cancellationToken).ConfigureAwait(false);
            await vault.ReplaceAsync(copy, dek, protector, cancellationToken).ConfigureAwait(false);
            return await ReportAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
            CryptographicOperations.ZeroMemory(plain);
            Delete(copy);
        }
    }

    private static async Task VerifyAsync(string file, byte[] dek, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await ConnectionFactory.OpenFileAsync(file, Convert.ToHexString(dek), cancellationToken).ConfigureAwait(false);
            await Schema.VerifyAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            throw new SealedBoxException("The database inside the backup does not open; the backup is damaged.", ex);
        }
    }

    private async Task<RestoreReport> ReportAsync(CancellationToken cancellationToken)
    {
        var accounts = await accountStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var posts = await postStore.ListByStatusAsync(Enum.GetValues<PostStatus>(), cancellationToken).ConfigureAwait(false);
        var open = new[] { PostStatus.Draft, PostStatus.Scheduled, PostStatus.Missed, PostStatus.Stopped, PostStatus.Blocked };
        var missing = posts.Where(p => open.Contains(p.Status)).SelectMany(p => p.Media).Select(m => m.Path)
            .Where(p => !File.Exists(p)).Distinct(StringComparer.Ordinal).ToList();
        logger.LogInformation("Restore finished: {Accounts} accounts, {Posts} posts, {Missing} missing media files", accounts.Count, posts.Count, missing.Count);
        return new RestoreReport(accounts.Count, posts.Count, missing);
    }

    private string TemporaryFile() => Path.Combine(paths.DataDirectory, "backup-" + Guid.NewGuid().ToString("N") + ".tmp");

    private void Delete(string file)
    {
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(file);
        }
        catch (IOException ex)
        {
            // The temporary copy is encrypted; it is only left behind when Windows still holds the file. Logged, not hidden.
            logger.LogWarning(ex, "Temporary backup file could not be deleted");
        }
    }
}
