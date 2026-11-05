using TMPublisher.Core.Settings;

namespace TMPublisher.Desktop.Startup;

/// <summary>Settings of the running app (loaded once after unlock, saved through here so every reader sees the change).</summary>
public sealed class AppSession(ISettingsStore store)
{
    /// <summary>Current settings (defaults until loaded).</summary>
    public AppSettings Settings { get; internal set; } = new();

    /// <summary>A message to show on the next page that has a status line (for example after a restore).</summary>
    public string? PendingMessage { get; set; }

    /// <summary>Raised after a save.</summary>
    public event EventHandler? Changed;

    /// <summary>Loads from the database.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken) => Settings = await store.GetAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Loads and returns the settings.</summary>
    public async Task<AppSettings> LoadAndGetAsync()
    {
        await LoadAsync(CancellationToken.None).ConfigureAwait(false);
        return Settings;
    }

    /// <summary>Saves and notifies.</summary>
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        Settings = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
