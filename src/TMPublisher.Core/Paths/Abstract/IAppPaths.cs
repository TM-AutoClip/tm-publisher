namespace TMPublisher.Core.Paths;

/// <summary>Where the app keeps its files (one data folder; overridable with <c>--data-dir</c> or <c>TMPUBLISHER_DATA_DIR</c>).</summary>
public interface IAppPaths
{
    /// <summary>Data folder.</summary>
    string DataDirectory { get; }

    /// <summary>Encrypted database file.</summary>
    string DatabaseFile { get; }

    /// <summary>File that holds the protected database key and how it is protected.</summary>
    string KeyFile { get; }

    /// <summary>Log folder.</summary>
    string LogDirectory { get; }
}
