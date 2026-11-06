using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Triggers;
using TMPublisher.Desktop.Composition;
using TMPublisher.Runtime.Paths;
using TMPublisher.Tests.Background;
using TMPublisher.Storage.Crypto;

namespace TMPublisher.Tests.Desktop;

/// <summary>The real composition root on a temporary data folder (cheap key derivation).</summary>
internal sealed class DesktopHarness : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "desktop-" + Guid.NewGuid().ToString("N"));

    public DesktopHarness(Action<IServiceCollection>? overrides = null)
    {
        Paths = new AppPaths(_folder);
        Services = DesktopServices.Build(Paths, s =>
        {
            s.AddSingleton(KdfParameters.Fast);

            // No real network in tests: every request gets the fake handler's "no route" answer.
            s.AddSingleton(new TMPublisher.Tests.Platforms.FakeHttp().Transport);

            // No test writes to the real system scheduler or looks at the real autostart entries.
            s.RemoveAll<ISystemTrigger>();
            s.AddSingleton<ISystemTrigger>(Trigger);
            s.RemoveAll<ILegacyAutostart>();
            overrides?.Invoke(s);
        });
    }

    public AppPaths Paths { get; }

    public FakeSystemTrigger Trigger { get; } = new();

    public ServiceProvider Services { get; }

    public void Dispose()
    {
        Services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Windows may still hold a file for a moment; the temp folder is cleaned later.
        }
    }
}
