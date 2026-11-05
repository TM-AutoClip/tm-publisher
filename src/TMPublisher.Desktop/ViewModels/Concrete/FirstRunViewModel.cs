using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Settings;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Scheduler;
using TMPublisher.Desktop.Startup;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;
using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// First run: language, then a new start (keyring protection or a master password; autostart on by default) or a restore from a backup.
/// Everything is in one window; a step cannot finish with a required value missing. The restore is also offered by the unlock window, while the
/// background scheduler may already run on the current data: it is stopped before the database is replaced, like Settings → Restore does.
/// </summary>
public sealed partial class FirstRunViewModel(Vault vault, BackupService backup, AppSession session, UiMasterPassword masterPassword,
    UiPreferencesFile preferences, IAutostart autostart, FileDialogs dialogs, SchedulerSupervisor supervisor, Localizer text,
    ILogger<FirstRunViewModel> logger) : ObservableObject
{
    /// <summary>Raised when the language was chosen (the window is rebuilt in that language).</summary>
    public event EventHandler? LanguageChosen;

    /// <summary>Raised when the database is open.</summary>
    public event EventHandler? Completed;

    /// <summary>Current page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLanguageStep), nameof(IsChoiceStep), nameof(IsNewStep), nameof(IsRestoreStep))]
    public partial FirstRunStep Step { get; set; }

    /// <summary>Language page.</summary>
    public bool IsLanguageStep => Step == FirstRunStep.Language;

    /// <summary>Choice page.</summary>
    public bool IsChoiceStep => Step == FirstRunStep.Choice;

    /// <summary>New start page.</summary>
    public bool IsNewStep => Step == FirstRunStep.New;

    /// <summary>Restore page.</summary>
    public bool IsRestoreStep => Step == FirstRunStep.Restore;

    /// <summary>Languages.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } = [new("en", "English"), new("tr", "Türkçe")];

    /// <summary>Chosen language.</summary>
    [ObservableProperty]
    public partial LanguageOption? Language { get; set; }

    /// <summary>Whether this computer has an automatic keyring (DPAPI / Secret Service).</summary>
    public bool KeyringAvailable => vault.AutomaticProtector is not null;

    /// <summary>Use a master password even though a keyring exists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsMasterPassword))]
    public partial bool UseMasterPassword { get; set; }

    /// <summary>Whether the master password fields are shown.</summary>
    public bool NeedsMasterPassword => UseMasterPassword || !KeyringAvailable;

    /// <summary>Master password.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordStrength))]
    public partial string MasterPassword { get; set; } = string.Empty;

    /// <summary>Master password again.</summary>
    [ObservableProperty]
    public partial string MasterPasswordRepeat { get; set; } = string.Empty;

    /// <summary>Strength 0-4.</summary>
    public int PasswordStrength => BackupPasswordPolicy.Strength(MasterPassword);

    /// <summary>Start with the system (default on).</summary>
    [ObservableProperty]
    public partial bool StartWithSystem { get; set; } = true;

    /// <summary>Backup file to restore.</summary>
    [ObservableProperty]
    public partial string BackupFile { get; set; } = string.Empty;

    /// <summary>Backup password.</summary>
    [ObservableProperty]
    public partial string BackupPassword { get; set; } = string.Empty;

    /// <summary>Error or status.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>A long operation runs.</summary>
    [ObservableProperty]
    public partial bool Busy { get; set; }

    /// <summary>Stores the language and rebuilds the window in it.</summary>
    [RelayCommand]
    private void ChooseLanguage()
    {
        var code = Language?.Code ?? "en";
        preferences.Write(new UiPreferences { Language = code });
        text.Use(code);
        LanguageChosen?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Goes to the new start page.</summary>
    [RelayCommand]
    private void ChooseNew() => Step = FirstRunStep.New;

    /// <summary>Goes to the restore page.</summary>
    [RelayCommand]
    private void ChooseRestore() => Step = FirstRunStep.Restore;

    /// <summary>Back to the choice.</summary>
    [RelayCommand]
    private void Back() => Step = FirstRunStep.Choice;

    /// <summary>Picks the backup file.</summary>
    [RelayCommand]
    private async Task BrowseBackupAsync()
    {
        if (await dialogs.OpenAsync("restore.pick", "backup.filter", ["*" + BackupService.Extension]).ConfigureAwait(true) is { } file)
        {
            BackupFile = file;
        }
    }

    /// <summary>Creates the encrypted database.</summary>
    [RelayCommand]
    public async Task CreateAsync()
    {
        if (!CheckMasterPassword())
        {
            return;
        }

        await RunAsync(async () =>
        {
            await vault.CreateAsync(Protector(), CancellationToken.None).ConfigureAwait(true);
            await FinishAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    /// <summary>Restores a backup.</summary>
    [RelayCommand]
    public async Task RestoreAsync()
    {
        if (BackupFile.Length == 0 || BackupPassword.Length == 0)
        {
            Message = text["restore.missing"];
            return;
        }

        if (!CheckMasterPassword())
        {
            return;
        }

        await RunAsync(async () =>
        {
            // A running scheduler would keep writing to the replaced file and keep the old key (every read would fail): stopped first, not
            // while a post is being sent. After the restore the main window starts it on the restored data and hands the new key over.
            var wasRunning = (await supervisor.RefreshAsync(CancellationToken.None).ConfigureAwait(true)).Status != SchedulerStatus.NotRunning;
            if (!await supervisor.StopAndWaitAsync(CancellationToken.None).ConfigureAwait(true))
            {
                Message = text["restore.scheduler_busy"];
                return;
            }

            RestoreReport report;
            try
            {
                report = await backup.RestoreAsync(BackupFile, BackupPassword, Protector(), CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // The previous database is back in place (the replace rolled back): the scheduler that ran on it starts again.
                if (wasRunning)
                {
                    await RestartSchedulerAsync().ConfigureAwait(true);
                }

                throw;
            }

            await FinishAsync().ConfigureAwait(true);
            logger.LogInformation("Restored {Accounts} accounts and {Posts} posts; {Missing} media files missing", report.Accounts, report.Posts, report.MissingFiles.Count);
            if (report.MissingFiles.Count > 0)
            {
                session.PendingMessage = text.Format("restore.missing_files", report.MissingFiles.Count, string.Join(", ", report.MissingFiles.Take(5).Select(Path.GetFileName)));
            }
        }).ConfigureAwait(true);
    }

    private async Task RestartSchedulerAsync()
    {
        try
        {
            var state = await supervisor.EnsureRunningAsync(CancellationToken.None).ConfigureAwait(true);
            logger.LogInformation("Background scheduler after the failed restore: {Status}", state.Status);
        }
        catch (Exception ex)
        {
            // The restore error is what the user sees; this one is kept in the log (the main window starts the scheduler again).
            logger.LogError(ex, "The background scheduler could not be started again after the failed restore");
        }
    }

    private IKeyProtector Protector()
    {
        if (NeedsMasterPassword)
        {
            masterPassword.Provide(MasterPassword);
            return vault.PasswordProtector;
        }

        return vault.AutomaticProtector!;
    }

    private bool CheckMasterPassword()
    {
        if (!NeedsMasterPassword)
        {
            return true;
        }

        if (!BackupPasswordPolicy.IsAcceptable(MasterPassword))
        {
            Message = text.Format("password.too_short", BackupPasswordPolicy.MinimumLength);
            return false;
        }

        if (MasterPassword != MasterPasswordRepeat)
        {
            Message = text["password.mismatch"];
            return false;
        }

        return true;
    }

    private async Task FinishAsync()
    {
        var settings = await session.LoadAndGetAsync().ConfigureAwait(true);
        settings.Language = text.Language;
        settings.StartWithSystem = StartWithSystem;
        await session.SaveAsync(settings, CancellationToken.None).ConfigureAwait(true);
        try
        {
            // Written once here; later only when the user changes the setting.
            autostart.Set(StartWithSystem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            logger.LogWarning(ex, "Autostart could not be set");
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync(Func<Task> action)
    {
        Busy = true;
        Message = string.Empty;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (SealedBoxException ex)
        {
            logger.LogWarning(ex, "Backup could not be opened");
            Message = text["restore.wrong_password"];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "First run step failed");
            Message = text.Format("error.generic", ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }
}
