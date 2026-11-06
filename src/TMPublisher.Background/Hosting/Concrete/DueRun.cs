using System.Globalization;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Localization;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Security;
using TMPublisher.Core.Settings;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Hosting;

/// <summary>
/// One run the system scheduler started (<c>--run-due</c>): becomes the sender of the data folder or ends at once (the window is open or another
/// run works), opens the database with the keyring, sends every post whose time has come (the claim, the five-minute grace, the retries and the
/// "never twice" rules of <see cref="PostScheduler"/>), does the due processing checks, sets the system scheduler's next start and ends. A
/// master-password database cannot be opened here: nothing is sent and, once per due post, a notification asks the user to open TM Publisher.
/// </summary>
public sealed class DueRun(AppPaths paths, Vault vault, SenderGate gate, PostScheduler scheduler, ProcessingWatcher processing, TriggerArmer armer, PauseSwitch pause,
    LegacyCleanup legacy, RunStateFile stateFile, ISettingsStore settings, TextCatalog text, INotifier notifier, TimeProvider clock, ILogger<DueRun> logger)
{
    /// <summary>Does the run.</summary>
    public async Task<RunOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        legacy.Run(mentionOldProgram: false);
        if (!await gate.TryTakeAsync(TriggerLimits.OldSchedulerWait, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Another sender works on this data folder (the open window or another run); this run ends without doing anything");
            return RunOutcome.OtherSender;
        }

        try
        {
            switch (vault.State)
            {
                case VaultState.NotCreated:
                    logger.LogInformation("No database yet (the first start in the window is not finished); nothing to send");
                    return RunOutcome.NoDatabase;
                case VaultState.Locked when vault.Method == KeyMethods.Password:
                    NoticeLocked(started);
                    return RunOutcome.Locked;
                case VaultState.Locked:
                    break;
            }

            var state = stateFile.Read();
            try
            {
                await vault.OpenAsync(cancellationToken).ConfigureAwait(false);
                text.Use((await settings.GetAsync(cancellationToken).ConfigureAwait(false)).Language);
                var paused = pause.IsPaused;
                if (paused)
                {
                    logger.LogInformation("Scheduling is paused; nothing is sent");
                }

                await scheduler.RunOnceAsync(started, paused, cancellationToken).ConfigureAwait(false);
                await processing.RunDueChecksAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Logged with its reason, shown once per run of errors; the next start comes after a growing wait (1, 2, 4, 8, then 15 minutes).
                state.Failures++;
                var wait = PostScheduler.Backoff(state.Failures);
                logger.LogError(ex, "The run could not handle the due posts (error {Count} in a row); the next start is in {Wait}", state.Failures, wait);
                if (state.Failures == 1)
                {
                    notifier.Notify(new AppNotification(NotificationKind.Failed, text["notify.scheduler_error"], Redactor.Redact(ex.Message)));
                }

                stateFile.Write(state);
                if (vault.State == VaultState.Open)
                {
                    await armer.ArmAsync(wait, cancellationToken).ConfigureAwait(false);
                }

                return RunOutcome.Failed;
            }

            if (state.Failures > 0)
            {
                logger.LogInformation("The due posts are handled again after {Count} failed runs", state.Failures);
                state.Failures = 0;
                stateFile.Write(state);
            }

            await armer.ArmAsync(scheduler.RetryAfter, cancellationToken).ConfigureAwait(false);
            return RunOutcome.Done;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// A master-password database: when the post the window last set the start for is due, the user is asked once (per due post) to open
    /// TM Publisher, which then sends it. Nothing is read from the database.
    /// </summary>
    private void NoticeLocked(DateTimeOffset now)
    {
        var due = armer.Current.NextPostUtc;
        if (due is not { } at || at > now)
        {
            logger.LogInformation("The database is protected with a master password; no post is due yet, nothing to do");
            return;
        }

        var state = stateFile.Read();
        if (state.UnlockNoticeForUtc == at)
        {
            logger.LogInformation("The database is protected with a master password; the post due at {Time} waits for the window (already notified)",
                at.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
            return;
        }

        logger.LogWarning("The database is protected with a master password, which a run cannot ask for; the post due at {Time} waits until TM Publisher is opened",
            at.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        text.Use(LanguageOfWindow());
        notifier.Notify(new AppNotification(NotificationKind.Info, text["notify.unlock_title"], text["notify.unlock_text"]));
        state.UnlockNoticeForUtc = at;
        stateFile.Write(state);
    }

    /// <summary>The language the window last used (the plain preferences file; the database cannot be read here).</summary>
    private string? LanguageOfWindow()
    {
        try
        {
            if (File.Exists(paths.PreferencesFile) && File.ReadAllText(paths.PreferencesFile).Contains("\"tr\"", StringComparison.Ordinal))
            {
                return "tr";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("The language preference could not be read ({Type}); English is used", ex.GetType().Name);
        }

        return null;
    }
}
