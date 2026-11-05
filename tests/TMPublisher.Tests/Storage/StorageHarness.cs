using Microsoft.Extensions.Logging.Abstractions;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Storage;

/// <summary>A vault with a master-password protector (works on every OS) in a temp folder.</summary>
internal sealed class StorageHarness : IDisposable
{
    public StorageHarness(string password = "correct horse battery")
    {
        Passwords = new FixedPasswords(password);
        Protector = new PasswordKeyProtector(Passwords, KdfParameters.Fast);
        Vault = new Vault(Paths, Key, [Protector], NullLogger<Vault>.Instance);
        Factory = new ConnectionFactory(Paths, Key);
    }

    public TempPaths Paths { get; } = new();

    public DatabaseKey Key { get; } = new();

    public FixedPasswords Passwords { get; }

    public PasswordKeyProtector Protector { get; }

    public Vault Vault { get; }

    public ConnectionFactory Factory { get; }

    public static async Task<StorageHarness> CreatedAsync()
    {
        var harness = new StorageHarness();
        await harness.Vault.CreateAsync(harness.Protector, CancellationToken.None);
        return harness;
    }

    public void Dispose() => Paths.Dispose();
}
