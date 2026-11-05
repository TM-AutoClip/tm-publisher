using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Posts;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Scheduler;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Desktop.Startup;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Settings: language, notifications, autostart, the background scheduler (state, start / stop, pause / resume), protection (keyring / master
/// password), backup and restore, about.
/// </summary>
public sealed partial class SettingsViewModel(AppSession session, UiPreferencesFile preferences, IAutostart autostart, Vault vault, BackupService backup,
    UiMasterPassword masterPassword, FileDialogs dialogs, IAppPaths paths, IPostScheduler scheduler, SchedulerSupervisor supervisor, TimeProvider clock,
    Localizer text, ILogger<SettingsViewModel> logger)
    : PageViewModelBase, IPageInfo
{
    private bool _watching;

    /// <inheritdoc />
    public static string TitleKey => "nav.settings";

    /// <inheritdoc />
    public static string Glyph => "⚙";

    /// <inheritdoc />
    public static int Order => 90;

    /// <summary>Languages.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } = [new("en", "English"), new("tr", "Türkçe")];

    /// <summary>Language.</summary>
    [ObservableProperty]
    public partial LanguageOption? Language { get; set; }

    /// <summary>Notifications on.</summary>
    [ObservableProperty]
    public partial bool NotificationsEnabled { get; set; }

    /// <summary>Notification duration (seconds).</summary>
    [ObservableProperty]
    public partial decimal NotificationSeconds { get; set; }

    /// <summary>Autostart.</summary>
    [ObservableProperty]
    public partial bool StartWithSystem { get; set; }

    /// <summary>TikTok drafts by default.</summary>
    [ObservableProperty]
    public partial bool TikTokDraftByDefault { get; set; }

    /// <summary>How the database key is protected now (localized).</summary>
    [ObservableProperty]
    public partial string Protection { get; set; } = string.Empty;

    /// <summary>Whether the master password protects the key now.</summary>
    [ObservableProperty]
    public partial bool UsesMasterPassword { get; set; }

    /// <summary>Whether a keyring is available here.</summary>
    public bool KeyringAvailable => vault.AutomaticProtector is not null;

    /// <summary>New master password.</summary>
    [ObservableProperty]
    public partial string NewMasterPassword { get; set; } = string.Empty;

    /// <summary>New master password again.</summary>
    [ObservableProperty]
    public partial string NewMasterPasswordRepeat { get; set; } = string.Empty;

    /// <summary>Backup password.</summary>
    [ObservableProperty]
    public partial string BackupPassword { get; set; } = string.Empty;

    /// <summary>Backup password again.</summary>
    [ObservableProperty]
    public partial string BackupPasswordRepeat { get; set; } = string.Empty;

    /// <summary>Password of the backup to restore.</summary>
    [ObservableProperty]
    public partial string RestorePassword { get; set; } = string.Empty;

    /// <summary>What the background scheduler does (localized).</summary>
    [ObservableProperty]
    public partial string SchedulerText { get; set; } = string.Empty;

    /// <summary>The next post and the reason of a problem (localized; empty when none).</summary>
    [ObservableProperty]
    public partial string SchedulerDetail { get; set; } = string.Empty;

    /// <summary>Whether the background scheduler runs.</summary>
    [ObservableProperty]
    public partial bool SchedulerRunning { get; set; }

    /// <summary>Whether sending is paused.</summary>
    [ObservableProperty]
    public partial bool SchedulerPaused { get; set; }

    /// <summary>Whether it can be paused now (running, not paused).</summary>
    [ObservableProperty]
    public partial bool SchedulerCanPause { get; set; }

    /// <summary>Data folder.</summary>
    public string DataFolder => paths.DataDirectory;

    /// <summary>App version.</summary>
    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? string.Empty;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        var s = session.Settings;
        Language = Languages.FirstOrDefault(l => l.Code == text.Language) ?? Languages[0];
        NotificationsEnabled = s.NotificationsEnabled;
        NotificationSeconds = s.NotificationSeconds;
        StartWithSystem = autostart.IsEnabled;
        TikTokDraftByDefault = s.TikTokDraftByDefault;
        UsesMasterPassword = vault.Method == KeyMethods.Password;
        Protection = text[UsesMasterPassword ? "settings.protection.password" : "settings.protection.keyring"];
        if (session.PendingMessage is { } pending)
        {
            Status = pending;
            session.PendingMessage = null;
        }

        if (!_watching)
        {
            _watching = true;
            supervisor.StateChanged += (_, _) => Dispatcher.UIThread.Post(ShowScheduler);
        }

        ShowScheduler();
        _ = supervisor.RefreshAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Starts the background scheduler (and hands the key over when needed).</summary>
    [RelayCommand]
    private async Task StartSchedulerAsync()
    {
        var state = await supervisor.EnsureRunningAsync(CancellationToken.None).ConfigureAwait(true);
        if (state.Status == SchedulerStatus.NotRunning)
        {
            Status = text.Format("startup.task_failed", state.Message);
        }
    }

    /// <summary>Stops the background scheduler (not while a post is being sent).</summary>
    [RelayCommand]
    private async Task StopSchedulerAsync()
    {
        Status = await supervisor.StopAndWaitAsync(CancellationToken.None).ConfigureAwait(true) ? text["scheduler.stopped"] : text["scheduler.busy"];
    }

    /// <summary>Pauses sending.</summary>
    [RelayCommand]
    private async Task PauseSchedulerAsync() => ShowReply(await supervisor.PauseAsync(CancellationToken.None).ConfigureAwait(true));

    /// <summary>Resumes sending.</summary>
    [RelayCommand]
    private async Task ResumeSchedulerAsync() => ShowReply(await supervisor.ResumeAsync(CancellationToken.None).ConfigureAwait(true));

    /// <summary>Reads the scheduler's state again.</summary>
    [RelayCommand]
    private Task RefreshSchedulerAsync() => supervisor.RefreshAsync(CancellationToken.None);

    private void ShowReply(SchedulerReply reply)
    {
        if (!reply.IsOk)
        {
            Status = text.Format("error.generic", reply.Text);
        }
    }

    private void ShowScheduler()
    {
        var state = supervisor.State;
        SchedulerText = text["scheduler.state." + state.Status.ToString().ToLowerInvariant()];
        SchedulerRunning = state.Status != SchedulerStatus.NotRunning;
        SchedulerPaused = state.Status == SchedulerStatus.Paused;
        SchedulerCanPause = state.Status == SchedulerStatus.Running;
        var details = new List<string>();
        if (state.NextPostUtc is { } next && state.Status is SchedulerStatus.Running or SchedulerStatus.Paused)
        {
            details.Add(text.Format("scheduler.next", TimeZoneInfo.ConvertTime(next, clock.LocalTimeZone).ToString("g", text.Culture)));
        }

        if (state.Message.Length > 0 && state.Status is SchedulerStatus.NotRunning or SchedulerStatus.Error)
        {
            details.Add(text.Format("scheduler.reason", state.Message));
        }

        SchedulerDetail = string.Join(Environment.NewLine, details);
    }

    /// <summary>Saves the general settings.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = session.Settings;
        var languageChanged = Language is not null && Language.Code != text.Language;
        s.Language = Language?.Code ?? s.Language;
        s.NotificationsEnabled = NotificationsEnabled;
        s.NotificationSeconds = (int)Math.Clamp(NotificationSeconds, 3, 3600);
        s.TikTokDraftByDefault = TikTokDraftByDefault;
        var autostartChanged = s.StartWithSystem != StartWithSystem || autostart.IsEnabled != StartWithSystem;
        s.StartWithSystem = StartWithSystem;
        try
        {
            await session.SaveAsync(s, CancellationToken.None).ConfigureAwait(true);
            preferences.Write(new UiPreferences { Language = s.Language });
            if (autostartChanged)
            {
                autostart.Set(StartWithSystem);
            }

            scheduler.Wake();
            Status = text[languageChanged ? "settings.saved_restart" : "settings.saved"];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Settings could not be saved");
            Status = text.Format("error.generic", ex.Message);
        }
    }

    /// <summary>Protects the key with a master password.</summary>
    [RelayCommand]
    private async Task SetMasterPasswordAsync()
    {
        if (!BackupPasswordPolicy.IsAcceptable(NewMasterPassword))
        {
            Status = text.Format("password.too_short", BackupPasswordPolicy.MinimumLength);
            return;
        }

        if (NewMasterPassword != NewMasterPasswordRepeat)
        {
            Status = text["password.mismatch"];
            return;
        }

        await ChangeAsync(() =>
        {
            masterPassword.Provide(NewMasterPassword);
            return vault.PasswordProtector;
        }).ConfigureAwait(true);
        NewMasterPassword = NewMasterPasswordRepeat = string.Empty;
    }

    /// <summary>Back to the automatic keyring.</summary>
    [RelayCommand]
    private Task UseKeyringAsync() => vault.AutomaticProtector is { } keyring ? ChangeAsync(() => keyring) : Task.CompletedTask;

    /// <summary>Writes a backup.</summary>
    [RelayCommand]
    private async Task BackupAsync()
    {
        if (!BackupPasswordPolicy.IsAcceptable(BackupPassword))
        {
            Status = text.Format("password.too_short", BackupPasswordPolicy.MinimumLength);
            return;
        }

        if (BackupPassword != BackupPasswordRepeat)
        {
            Status = text["password.mismatch"];
            return;
        }

        var file = await dialogs.SaveAsync("backup.save", "tm-publisher-" + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            "backup.filter", BackupService.Extension).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        try
        {
            await backup.CreateAsync(file, BackupPassword, CancellationToken.None).ConfigureAwait(true);
            BackupPassword = BackupPasswordRepeat = string.Empty;
            Status = text.Format("backup.done", Path.GetFileName(file));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed");
            Status = text.Format("error.generic", ex.Message);
        }
    }

    /// <summary>Restores a backup over the current data (the current database is moved aside, not deleted).</summary>
    [RelayCommand]
    private async Task RestoreAsync()
    {
        var file = await dialogs.OpenAsync("restore.pick", "backup.filter", ["*" + BackupService.Extension]).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        if (UsesMasterPassword)
        {
            // The restored key is sealed again with the current master password protection: the user types it as the restore password's pair.
            Status = text["restore.master_password_note"];
            return;
        }

        // The background scheduler must not write to the database while it is replaced: it is stopped first (not while a post is being sent)
        // and started again on the restored data.
        if (!await supervisor.StopAndWaitAsync(CancellationToken.None).ConfigureAwait(true))
        {
            Status = text["restore.scheduler_busy"];
            return;
        }

        try
        {
            var report = await backup.RestoreAsync(file, RestorePassword, vault.AutomaticProtector!, CancellationToken.None).ConfigureAwait(true);
            await session.LoadAsync(CancellationToken.None).ConfigureAwait(true);

            // The background scheduler starts again on the restored data (interrupted posts, follow-ups and the timer are handled at its start).
            await scheduler.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
            RestorePassword = string.Empty;
            Status = report.MissingFiles.Count == 0
                ? text.Format("restore.done", report.Accounts, report.Posts)
                : text.Format("restore.missing_files", report.MissingFiles.Count, string.Join(", ", report.MissingFiles.Take(5).Select(Path.GetFileName)));
            await ActivateAsync().ConfigureAwait(true);
        }
        catch (SealedBoxException ex)
        {
            logger.LogWarning(ex, "Restore failed");
            Status = text["restore.wrong_password"];
            await scheduler.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore failed");
            Status = text.Format("error.generic", ex.Message);
            await scheduler.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async Task ChangeAsync(Func<IKeyProtector> protector)
    {
        try
        {
            await vault.ChangeProtectionAsync(protector(), CancellationToken.None).ConfigureAwait(true);

            // The running scheduler keeps its key; one waiting for it gets it now.
            await supervisor.EnsureRunningAsync(CancellationToken.None).ConfigureAwait(true);
            await ActivateAsync().ConfigureAwait(true);
            Status = text["settings.protection.changed"];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Key protection could not be changed");
            Status = text.Format("error.generic", ex.Message);
        }
    }
}
