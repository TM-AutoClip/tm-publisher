using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Paths;
using TMPublisher.Storage.Database;

namespace TMPublisher.Storage.Keys;

/// <summary>
/// Creates, opens and re-protects the encrypted database. A new 32-byte random key (DEK) opens the database; the DEK is stored only sealed
/// by a protector (DPAPI on Windows, Secret Service on Linux, or a master password). The user does not type anything when an operating
/// system protector is used.
/// </summary>
public sealed class Vault(IAppPaths paths, DatabaseKey key, IEnumerable<IKeyProtector> protectors, ILogger<Vault> logger)
{
    private static readonly string[] DatabaseParts = [string.Empty, "-wal", "-shm"];
    private readonly List<IKeyProtector> _protectors = protectors.ToList();

    /// <summary>Current state.</summary>
    public VaultState State => key.IsLoaded ? VaultState.Open : File.Exists(paths.KeyFile) ? VaultState.Locked : VaultState.NotCreated;

    /// <summary>The automatic protector of this computer (DPAPI / Secret Service), or <c>null</c> when none works.</summary>
    public IKeyProtector? AutomaticProtector => _protectors.FirstOrDefault(p => p.Automatic && p.IsAvailable);

    /// <summary>The master password protector.</summary>
    public IKeyProtector PasswordProtector => _protectors.First(p => p.Method == KeyMethods.Password);

    /// <summary>Method used by the current key file (<c>null</c> before creation).</summary>
    public string? Method => KeyFile.Read(paths.KeyFile)?.Method;

    /// <summary>First start: a new key, sealed with <paramref name="protector"/>, and an empty database with the schema.</summary>
    public async Task CreateAsync(IKeyProtector protector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protector);
        if (State != VaultState.NotCreated)
        {
            throw new InvalidOperationException("The database already exists.");
        }

        var dek = RandomNumberGenerator.GetBytes(32);
        try
        {
            var envelope = await protector.ProtectAsync(dek, cancellationToken).ConfigureAwait(false);
            if (File.Exists(paths.DatabaseFile))
            {
                // A database without a key file cannot be opened by anyone; it is moved aside, never deleted.
                MoveDatabaseAside(".orphan-" + Stamp());
            }

            key.Load(dek);
            try
            {
                await using (var context = new PublisherDbContextFactory(paths, key).CreateDbContext())
                {
                    await Schema.EnsureAsync(context, cancellationToken).ConfigureAwait(false);
                }

                KeyFile.Write(paths.KeyFile, envelope);
            }
            catch (Exception ex)
            {
                // Nothing half-made stays: the key is forgotten and the new empty file removed, so the user can fix the cause and try again.
                logger.LogError(ex, "Creating the encrypted database failed ({Type}); nothing was kept", ex.GetType().Name);
                key.Clear();
                SqliteConnection.ClearAllPools();
                if (!File.Exists(paths.KeyFile))
                {
                    DeleteDatabaseFiles();
                }

                throw;
            }

            logger.LogInformation("Encrypted database created (key protection: {Method})", protector.Method);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>Opens the database with the key file's protector; an exception with the reason when it cannot.</summary>
    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        var envelope = KeyFile.Read(paths.KeyFile) ?? throw new InvalidOperationException("There is no key file; the database was never created here.");
        if (!File.Exists(paths.DatabaseFile))
        {
            logger.LogCritical("The key file exists but the database file {File} is missing; nothing is created", Path.GetFileName(paths.DatabaseFile));
            throw new InvalidOperationException("The database file is missing although its key is here; no new empty database was made. Restore a backup, "
                + "or put the database file back (a restore keeps the previous one as publisher.db.before-restore-<time>).");
        }

        var protector = _protectors.FirstOrDefault(p => p.Method == envelope.Method)
            ?? throw new InvalidOperationException($"The key file uses '{envelope.Method}', which this app does not know.");
        if (!protector.IsAvailable)
        {
            throw new InvalidOperationException($"The key protection '{envelope.Method}' is not available on this computer now.");
        }

        var dek = await protector.UnprotectAsync(envelope, cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadAndVerifyAsync(dek, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }

        logger.LogInformation("Encrypted database opened (key protection: {Method})", envelope.Method);
    }

    /// <summary>
    /// Opens the database with a key the window handed over (the background scheduler cannot ask for a master password). The key is checked
    /// by reading the database; a wrong key leaves the vault locked.
    /// </summary>
    public async Task OpenWithKeyAsync(byte[] dek, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dek);
        if (!File.Exists(paths.DatabaseFile))
        {
            logger.LogCritical("A key was handed over but the database file {File} is missing; nothing is created", Path.GetFileName(paths.DatabaseFile));
            throw new InvalidOperationException("The database file is missing; no new empty database was made.");
        }

        await LoadAndVerifyAsync(dek, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Encrypted database opened with the key handed over by the window");
    }

    private async Task LoadAndVerifyAsync(byte[] dek, CancellationToken cancellationToken)
    {
        key.Load(dek);
        try
        {
            await using var context = new PublisherDbContextFactory(paths, key).CreateDbContext();
            await Schema.EnsureAsync(context, cancellationToken).ConfigureAwait(false);
            await Schema.VerifyAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            key.Clear();
            throw;
        }
    }

    /// <summary>Seals the open key with another protector (for example "lock with a master password") and forgets the old seal.</summary>
    public async Task ChangeProtectionAsync(IKeyProtector protector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protector);
        var old = KeyFile.Read(paths.KeyFile);
        var dek = key.Copy();
        try
        {
            KeyFile.Write(paths.KeyFile, await protector.ProtectAsync(dek, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }

        if (old is not null && _protectors.FirstOrDefault(p => p.Method == old.Method) is { } previous)
        {
            await previous.ForgetAsync(old, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Key protection changed to {Method}", protector.Method);
    }

    /// <summary>
    /// Replaces the database with <paramref name="databaseFile"/> and its key (restore). Whatever can fail (sealing the key, copying the
    /// file) runs before the current files are touched. The current database (with its <c>-wal</c> / <c>-shm</c> files) and its key file are
    /// kept under the same <c>.before-restore-&lt;time&gt;</c> name, never deleted, so the previous data can still be opened. Any failure puts
    /// the previous files and key back. Returns the name the previous database was kept under (empty when there was none).
    /// </summary>
    public async Task<string> ReplaceAsync(string databaseFile, byte[] dek, IKeyProtector protector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(dek);
        var envelope = await protector.ProtectAsync(dek, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(paths.DatabaseFile))!);
        var suffix = ".before-restore-" + Stamp();
        var staged = paths.DatabaseFile + ".restore.tmp";
        File.Copy(databaseFile, staged, overwrite: true);

        var previous = key.IsLoaded ? key.Copy() : null;
        var moved = new List<(string Live, string Aside)>();
        var keyAside = paths.KeyFile + suffix;
        var hadKey = File.Exists(paths.KeyFile);
        var placed = false;
        var keyWritten = false;
        try
        {
            key.Clear();
            SqliteConnection.ClearAllPools();
            if (hadKey)
            {
                File.Copy(paths.KeyFile, keyAside, overwrite: true);
            }

            moved.AddRange(MoveDatabaseAside(suffix));
            File.Move(staged, paths.DatabaseFile);
            placed = true;
            KeyFile.Write(paths.KeyFile, envelope);
            keyWritten = true;
            key.Load(dek);
            logger.LogInformation("Database replaced from a backup (key protection: {Method}); the previous one is kept as {Aside}", protector.Method,
                moved.Count > 0 ? Path.GetFileName(moved[0].Aside) : "(none)");
            return moved.Count > 0 ? Path.GetFileName(moved[0].Aside) : string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Replacing the database failed ({Type}); putting the previous database and key back", ex.GetType().Name);
            RollBack(moved, placed, keyWritten, hadKey ? keyAside : null, previous);
            throw;
        }
        finally
        {
            if (previous is not null)
            {
                CryptographicOperations.ZeroMemory(previous);
            }

            if (File.Exists(staged))
            {
                TryDelete(staged);
            }
        }
    }

    /// <summary>Forgets the key in memory (the database is locked again).</summary>
    public void Lock() => key.Clear();

    private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Moves the database and its -wal / -shm files to the same name + <paramref name="suffix"/>; returns what was moved.</summary>
    private List<(string Live, string Aside)> MoveDatabaseAside(string suffix)
    {
        var moved = new List<(string Live, string Aside)>();
        foreach (var part in DatabaseParts)
        {
            var live = paths.DatabaseFile + part;
            if (File.Exists(live))
            {
                var aside = paths.DatabaseFile + suffix + part;
                File.Move(live, aside);
                moved.Add((live, aside));
            }
        }

        return moved;
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var part in DatabaseParts)
        {
            if (File.Exists(paths.DatabaseFile + part))
            {
                TryDelete(paths.DatabaseFile + part);
            }
        }
    }

    private void RollBack(List<(string Live, string Aside)> moved, bool placed, bool keyWritten, string? keyAside, byte[]? previous)
    {
        // Every step is tried and logged on its own: one failing step must not keep the others from putting the user's data back.
        key.Clear();
        SqliteConnection.ClearAllPools();
        if (placed)
        {
            DeleteDatabaseFiles();
        }

        foreach (var (live, aside) in Enumerable.Reverse(moved))
        {
            try
            {
                File.Move(aside, live, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogCritical(ex, "Could not put {File} back; it is still at {Aside}", Path.GetFileName(live), Path.GetFileName(aside));
            }
        }

        var keyBack = true;
        if (keyWritten)
        {
            try
            {
                if (keyAside is not null)
                {
                    File.Copy(keyAside, paths.KeyFile, overwrite: true);
                }
                else
                {
                    File.Delete(paths.KeyFile);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                keyBack = false;
                logger.LogCritical(ex, "Could not put the previous key file back; it is still at {Aside}", keyAside is null ? "(none)" : Path.GetFileName(keyAside));
            }
        }

        if (keyBack && keyAside is not null && File.Exists(keyAside))
        {
            // The live key file is the previous one again; its spare copy is not needed.
            TryDelete(keyAside);
        }

        if (previous is not null)
        {
            key.Load(previous);
        }
    }

    private void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete {File}", Path.GetFileName(file));
        }
    }
}
