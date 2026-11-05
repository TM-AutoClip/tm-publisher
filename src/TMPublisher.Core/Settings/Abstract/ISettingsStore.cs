namespace TMPublisher.Core.Settings;

/// <summary>Reads and writes <see cref="AppSettings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Current settings (defaults when nothing is stored).</summary>
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Stores the settings.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
