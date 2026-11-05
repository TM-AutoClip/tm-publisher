using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Desktop.Composition;
using TMPublisher.Desktop.Paths;
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
            overrides?.Invoke(s);
        });
    }

    public AppPaths Paths { get; }

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
