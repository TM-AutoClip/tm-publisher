using Microsoft.Data.Sqlite;
using TMPublisher.Core.Paths;

namespace TMPublisher.Tests.Storage;

/// <summary>A throw-away data folder.</summary>
internal sealed class TempPaths : IAppPaths, IDisposable
{
    public TempPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }

    public string DatabaseFile => Path.Combine(DataDirectory, "publisher.db");

    public string KeyFile => Path.Combine(DataDirectory, "key.json");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A file may still be open on Windows for a moment; the temp folder is cleaned by the system later.
        }
    }
}
