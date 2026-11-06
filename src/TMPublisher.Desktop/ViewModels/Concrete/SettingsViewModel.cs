using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Paths;
using TMPublisher.Core.Posts;
using TMPublisher.Background.Triggers;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Sending;
using TMPublisher.Desktop.Startup;
using TMPublisher.Storage.Backup;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Settings: language, notifications, sending while the app is closed (the system scheduler's entry and its state), pause / resume, protection
/// (keyring / master password), backup and restore, about.
/// </summary>
public sealed partial class SettingsViewModel(AppSession session, UiPreferencesFile preferences, Vault vault, BackupService backup,
    UiMasterPassword masterPassword, FileDialogs dialogs, IAppPaths paths, IPostScheduler scheduler, WindowSender sender, TimeProvider clock,
    Localizer text, ILogger<SettingsViewModel> logger)
    : PageViewModelBase, IPageInfo
{
    private static readonly TimeSpan RestoreWait = TimeSpan.FromSeconds(30);
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

    /// <summary>Send the scheduled posts while the app is closed (the system scheduler starts it for a moment).</summary>
    [ObservableProperty]
    public partial bool StartWithSystem { get; set; }

    /// <summary>Wake the computer from sleep to send (Windows).</summary>
    [ObservableProperty]
    public partial bool WakeToRun { get; set; }

    /// <summary>Whether waking the computer can be chosen here (Windows Task Scheduler).</summary>
    public static bool WakeAvailable => OperatingSystem.IsWindows();

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

    /// <summary>Who sends now (localized): this window, paused, or a run that is still sending.</summary>
    [ObservableProperty]
    public partial string SchedulerText { get; set; } = string.Empty;

    /// <summary>The next post and the system scheduler's entry (localized; empty when none).</summary>
    [ObservableProperty]
    public partial string SchedulerDetail { get; set; } = string.Empty;

    /// <summary>Whether sending is paused.</summary>
    [ObservableProperty]
    public partial bool SchedulerPaused { get; set; }

    /// <summary>Whether it can be paused now (not paused).</summary>
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
        StartWithSystem = s.StartWithSystem;
        WakeToRun = s.WakeToRun;
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
            sender.StateChanged += (_, _) => Dispatcher.UIThread.Post(ShowScheduler);
        }

        ShowScheduler();
        return Task.CompletedTask;
    }

    /// <summary>Pauses sending (also while the window is closed).</summary>
    [RelayCommand]
    private Task PauseSchedulerAsync() => SetPausedAsync(true);

    /// <summary>Resumes sending.</summary>
    [RelayCommand]
    private Task ResumeSchedulerAsync() => SetPausedAsync(false);

    private async Task SetPausedAsync(bool paused)
    {
        try
        {
            await sender.SetPausedAsync(paused).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The pause state could not be stored");
            Status = text.Format("error.generic", ex.Message);
        }

        ShowScheduler();
    }

    private void ShowScheduler()
    {
        SchedulerPaused = sender.IsPaused;
        SchedulerCanPause = !sender.IsPaused;
        SchedulerText = text[sender.IsPaused ? "sending.paused" : sender.IsSending ? "sending.here" : "sending.waiting"];
        var details = new List<string>();
        if (sender.NextPostUtc is { } next)
        {
            details.Add(text.Format("scheduler.next", Local(next)));
        }

        var trigger = sender.Trigger;
        details.Add(trigger.State switch
        {
            TriggerState.On when trigger.NextRunUtc is { } run => text.Format("trigger.on", sender.TriggerKind, sender.TriggerName, Local(run)),
            TriggerState.On => text.Format("trigger.on_sign_in", sender.TriggerKind, sender.TriggerName),
            TriggerState.SignInOnly => text["trigger.sign_in_only"],
            TriggerState.Failed => text.Format("trigger.failed", trigger.Message),
            _ => text["trigger.off"],
        });
        SchedulerDetail = string.Join(Environment.NewLine, details);
    }

    private string Local(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, clock.LocalTimeZone).ToString("g", text.Culture);

    /// <summary>Saves the general settings.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = session.Settings;
        var languageChanged = Language is not null && Language.Code != text.Language;
        var changed = new List<string>();
        if (Language is not null && Language.Code != s.Language)
        {
            changed.Add("language");
        }

        if (s.NotificationsEnabled != NotificationsEnabled || s.NotificationSeconds != (int)Math.Clamp(NotificationSeconds, 3, 3600))
        {
            changed.Add("notifications");
        }

        if (s.TikTokDraftByDefault != TikTokDraftByDefault)
        {
            changed.Add("tiktok draft default");
        }

        if (s.StartWithSystem != StartWithSystem)
        {
            changed.Add("send while closed");
        }

        if (s.WakeToRun != WakeToRun)
        {
            changed.Add("wake to send");
        }

        s.Language = Language?.Code ?? s.Language;
        s.NotificationsEnabled = NotificationsEnabled;
        s.NotificationSeconds = (int)Math.Clamp(NotificationSeconds, 3, 3600);
        s.TikTokDraftByDefault = TikTokDraftByDefault;
        s.StartWithSystem = StartWithSystem;
        s.WakeToRun = WakeToRun && WakeAvailable;
        try
        {
            await session.SaveAsync(s, CancellationToken.None).ConfigureAwait(true);
            preferences.Write(new UiPreferences { Language = s.Language });

            // Reads the schedule again and writes (or removes) the system scheduler's entry with the new settings.
            scheduler.Wake();
            logger.LogInformation("Settings saved (changed: {Changed})", changed.Count == 0 ? "nothing" : string.Join(", ", changed));
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

        // Nothing may send while the database is replaced: this window stops sending first (not while a post is being sent; a run of the
        // system scheduler that still sends is waited for) and starts again on the restored data.
        if (!await sender.StopForRestoreAsync(RestoreWait).ConfigureAwait(true))
        {
            Status = text["restore.scheduler_busy"];
            return;
        }

        try
        {
            var report = await backup.RestoreAsync(file, RestorePassword, vault.AutomaticProtector!, CancellationToken.None).ConfigureAwait(true);
            await session.LoadAsync(CancellationToken.None).ConfigureAwait(true);

            // Sending starts again on the restored data (interrupted posts, follow-ups and the timer are handled at its start).
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

            // A run cannot open a master-password database; the window sends while it is open and tells the user when a post waits for it.
            scheduler.Wake();
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
