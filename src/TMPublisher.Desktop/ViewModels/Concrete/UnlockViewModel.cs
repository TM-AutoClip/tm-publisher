using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Startup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Opens a locked database: with the master password, or again with the keyring after an error; or switches to a restore.</summary>
public sealed partial class UnlockViewModel(Vault vault, UiMasterPassword masterPassword, Localizer text, ILogger<UnlockViewModel> logger) : ObservableObject
{
    /// <summary>Raised when the database is open.</summary>
    public event EventHandler? Completed;

    /// <summary>Raised when the user wants to restore a backup instead.</summary>
    public event EventHandler? RestoreRequested;

    /// <summary>Whether the database is protected by a master password.</summary>
    public bool NeedsPassword => vault.Method == KeyMethods.Password;

    /// <summary>Password.</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Error.</summary>
    [ObservableProperty]
    public partial string Error { get; set; } = string.Empty;

    /// <summary>Running.</summary>
    [ObservableProperty]
    public partial bool Busy { get; set; }

    /// <summary>Opens.</summary>
    [RelayCommand]
    public async Task UnlockAsync()
    {
        Busy = true;
        Error = string.Empty;
        try
        {
            if (NeedsPassword)
            {
                masterPassword.Provide(Password);
            }

            await vault.OpenAsync(CancellationToken.None).ConfigureAwait(true);
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (SealedBoxException ex)
        {
            logger.LogWarning(ex, "Wrong master password");
            Error = text["unlock.wrong_password"];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unlock failed");
            Error = text.Format("error.generic", ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Switches to restoring a backup.</summary>
    [RelayCommand]
    private void Restore() => RestoreRequested?.Invoke(this, EventArgs.Empty);
}
