using System.Text;
using Microsoft.Data.Sqlite;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Tests.Storage;

public sealed class EncryptionTests
{
    [Fact]
    public async Task DatabaseFileIsEncryptedAndDoesNotOpenWithAnotherKey()
    {
        using var h = await StorageHarness.CreatedAsync();
        await new AccountStore(h.Factory).SaveAsync(new Account { Platform = "x", DisplayName = "secret-name-123" }, CancellationToken.None);
        SqliteConnection.ClearAllPools();

        var bytes = await File.ReadAllBytesAsync(h.Paths.DatabaseFile, TestContext.Current.CancellationToken);
        Encoding.ASCII.GetString(bytes, 0, 15).ShouldNotBe("SQLite format 3");
        Encoding.UTF8.GetString(bytes).ShouldNotContain("secret-name-123");
        Encoding.UTF8.GetString(bytes).ShouldNotContain("accounts");

        await Should.ThrowAsync<SqliteException>(async () =>
        {
            await using var other = PublisherDbContextFactory.ForFile(h.Paths.DatabaseFile, Convert.ToHexString(new byte[32]));
            await Schema.VerifyAsync(other, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task AMissingDatabaseIsAnErrorNotANewEmptyOne()
    {
        using var h = await StorageHarness.CreatedAsync();
        h.Vault.Lock();
        SqliteConnection.ClearAllPools();
        File.Move(h.Paths.DatabaseFile, h.Paths.DatabaseFile + ".elsewhere");

        var error = await Should.ThrowAsync<InvalidOperationException>(() => h.Vault.OpenAsync(CancellationToken.None));
        error.Message.ShouldContain("missing");
        File.Exists(h.Paths.DatabaseFile).ShouldBeFalse();
        h.Vault.State.ShouldBe(VaultState.Locked);
    }

    [Fact]
    public async Task AFailedCreateCanBeTriedAgain()
    {
        using var h = new StorageHarness();

        // The database cannot be made (a folder sits where its file goes): the vault must not stay "open" without a key file.
        Directory.CreateDirectory(h.Paths.DatabaseFile);
        await Should.ThrowAsync<Exception>(() => h.Vault.CreateAsync(h.Protector, CancellationToken.None));
        h.Vault.State.ShouldBe(VaultState.NotCreated);
        File.Exists(h.Paths.KeyFile).ShouldBeFalse();

        Directory.Delete(h.Paths.DatabaseFile);
        await h.Vault.CreateAsync(h.Protector, CancellationToken.None);
        h.Vault.State.ShouldBe(VaultState.Open);
    }

    [Fact]
    public async Task VaultOpensAgainWithTheRightPasswordOnly()
    {
        using var h = await StorageHarness.CreatedAsync();
        h.Vault.Lock();
        h.Vault.State.ShouldBe(VaultState.Locked);

        h.Passwords.Password = "wrong password!!";
        await Should.ThrowAsync<SealedBoxException>(() => h.Vault.OpenAsync(CancellationToken.None));
        h.Vault.State.ShouldBe(VaultState.Locked);

        h.Passwords.Password = "correct horse battery";
        await h.Vault.OpenAsync(CancellationToken.None);
        h.Vault.State.ShouldBe(VaultState.Open);
    }

    [Fact]
    public async Task DpapiProtectorRoundTripsOnWindows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists only on Windows.");
        var protector = new DpapiKeyProtector();
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var envelope = await protector.ProtectAsync(key, CancellationToken.None);
        envelope.Payload.ShouldNotContain(Convert.ToBase64String(key));
        (await protector.UnprotectAsync(envelope, CancellationToken.None)).ShouldBe(key);
    }

    [Fact]
    public async Task SecretServiceSendsTheKeyOnStandardInputOnly()
    {
        var runner = new FakeCommandRunner();
        var protector = new SecretServiceKeyProtector(runner);
        var key = Enumerable.Range(0, 32).Select(i => (byte)(i * 3)).ToArray();
        var envelope = await protector.ProtectAsync(key, CancellationToken.None);
        runner.Calls.Single().Arguments.ShouldNotContain(a => a.Contains(Convert.ToBase64String(key), StringComparison.Ordinal));
        runner.Calls.Single().Input.ShouldBe(Convert.ToBase64String(key));
        runner.Stored = Convert.ToBase64String(key);
        (await protector.UnprotectAsync(envelope, CancellationToken.None)).ShouldBe(key);
    }

    [Fact]
    public void SealedBoxRejectsWrongPasswordAndChangedHeader()
    {
        var magic = "TMPTEST1"u8.ToArray();
        var sealedData = SealedBox.Seal(magic, "pass", "hello"u8, KdfParameters.Fast);
        SealedBox.Open(magic, "pass", sealedData).ShouldBe("hello"u8.ToArray());
        Should.Throw<SealedBoxException>(() => SealedBox.Open(magic, "other", sealedData));
        var changed = (byte[])sealedData.Clone();
        changed[14] ^= 1;
        Should.Throw<SealedBoxException>(() => SealedBox.Open(magic, "pass", changed));
    }
}
