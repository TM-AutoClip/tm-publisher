using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Tests.Storage;

public sealed class BackupTests
{
    private const string Password = "a long backup password";

    private static BackupService Service(StorageHarness h) => new(h.Paths, h.Key, h.Vault, KdfParameters.Fast, NullLogger<BackupService>.Instance);

    private static async Task<string> BackupWithDataAsync(StorageHarness h)
    {
        await new AccountStore(h.Factory).SaveAsync(new Account { Platform = "mastodon", DisplayName = "Backup me" }, CancellationToken.None);
        await new CredentialStore(h.Factory).SaveAsync(Guid.NewGuid(), Credentials.Empty.With(("access_token", "tok")), CancellationToken.None);
        var post = new Post { Kind = PostKind.Photo, Status = PostStatus.Scheduled, Media = [new MediaItem(Path.Combine(h.Paths.DataDirectory, "gone.jpg"), MediaType.Photo, 1)] };
        await new PostStore(h.Factory, TimeProvider.System).SaveAsync(post, CancellationToken.None);
        var file = Path.Combine(h.Paths.DataDirectory, "test" + BackupService.Extension);
        await Service(h).CreateAsync(file, Password, CancellationToken.None);
        return file;
    }

    [Fact]
    public async Task BackupRestoresOnAnotherComputer()
    {
        using var source = await StorageHarness.CreatedAsync();
        var file = await BackupWithDataAsync(source);
        var bytes = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(bytes).ShouldNotContain("Backup me");

        using var target = new StorageHarness("another machine password");
        var report = await Service(target).RestoreAsync(file, Password, target.Protector, CancellationToken.None);
        report.Accounts.ShouldBe(1);
        report.Posts.ShouldBe(1);
        report.MissingFiles.Single().ShouldEndWith("gone.jpg");
        (await new AccountStore(target.Factory).ListAsync(CancellationToken.None)).Single().DisplayName.ShouldBe("Backup me");

        // The restored key is now sealed with the new computer's protector.
        target.Vault.Lock();
        await target.Vault.OpenAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WrongPasswordDamagedFileAndChangedHeaderAreRejected()
    {
        using var source = await StorageHarness.CreatedAsync();
        var file = await BackupWithDataAsync(source);
        using var target = new StorageHarness();
        await Should.ThrowAsync<SealedBoxException>(() => Service(target).RestoreAsync(file, "not the password", target.Protector, CancellationToken.None));

        var bytes = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
        var damaged = (byte[])bytes.Clone();
        damaged[^40] ^= 0xFF;
        var damagedFile = file + ".damaged";
        await File.WriteAllBytesAsync(damagedFile, damaged, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<SealedBoxException>(() => Service(target).RestoreAsync(damagedFile, Password, target.Protector, CancellationToken.None));

        var header = (byte[])bytes.Clone();
        header[13] ^= 0x01;
        var headerFile = file + ".header";
        await File.WriteAllBytesAsync(headerFile, header, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<SealedBoxException>(() => Service(target).RestoreAsync(headerFile, Password, target.Protector, CancellationToken.None));

        File.Exists(target.Paths.DatabaseFile).ShouldBeFalse();
    }

    [Fact]
    public async Task ShortPasswordIsRefused()
    {
        using var h = await StorageHarness.CreatedAsync();
        await Should.ThrowAsync<ArgumentException>(() => Service(h).CreateAsync(Path.Combine(h.Paths.DataDirectory, "x.tmpbak"), "short", CancellationToken.None));
        BackupPasswordPolicy.Strength("short").ShouldBe(1);
        BackupPasswordPolicy.Strength("Long-Password-2026!").ShouldBe(4);
    }

    [Fact]
    public async Task TemporaryCopyIsEncrypted()
    {
        using var h = await StorageHarness.CreatedAsync();
        var passphrase = h.Key.Passphrase();
        var copy = Path.Combine(h.Paths.DataDirectory, "copy.db");
        using (var source = new SqliteConnection(TMPublisher.Storage.Database.PublisherDbContextFactory.ConnectionString(h.Paths.DatabaseFile, passphrase)))
        using (var destination = new SqliteConnection(TMPublisher.Storage.Database.PublisherDbContextFactory.ConnectionString(copy, passphrase)))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        SqliteConnection.ClearAllPools();
        var head = await File.ReadAllBytesAsync(copy, TestContext.Current.CancellationToken);
        Encoding.ASCII.GetString(head, 0, 15).ShouldNotBe("SQLite format 3");
    }
}
