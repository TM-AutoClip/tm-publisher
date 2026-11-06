using System.Text.Json;
using Microsoft.Extensions.Logging;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Serialization;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// Sets the system scheduler's next start of the data folder: after every pass of the open window (a post saved, moved, sent; a setting
/// changed) and at the end of every run. The next start is the earliest of the next scheduled post, the next processing check of a sent video
/// and a retry; nothing when paused (only the sign-in start stays). "Send while closed" off removes the entry. An unchanged definition is not
/// written again, so the system scheduler is only touched when something changed. What was written is kept in <c>system-trigger.json</c>.
/// </summary>
public sealed class TriggerArmer(AppPaths paths, ISystemTrigger trigger, IPostStore posts, ISettingsStore settings, ProcessingWatcher processing,
    PauseSwitch pause, TimeProvider clock, ILogger<TriggerArmer> logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The program file the entry starts (replaced in tests, where the test host is running).</summary>
    internal Func<string?> ProgramFile { get; set; } = () => ProgramFiles.Self;

    /// <summary>The entry's name.</summary>
    public string Name => TriggerNames.For(paths);

    /// <summary>The system scheduler's kind.</summary>
    public string Kind => trigger.Kind;

    private string RecordFile => Path.Combine(paths.DataDirectory, "system-trigger.json");

    /// <summary>What was written last (state <see cref="TriggerState.Off"/> when nothing was).</summary>
    public TriggerRecord Current
    {
        get
        {
            try
            {
                return File.Exists(RecordFile)
                    ? JsonSerializer.Deserialize(File.ReadAllText(RecordFile), BackgroundJsonContext.Default.TriggerRecord) ?? new TriggerRecord()
                    : new TriggerRecord();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                logger.LogWarning("The record of the system scheduler entry could not be read ({Type}); it is written again", ex.GetType().Name);
                return new TriggerRecord();
            }
        }
    }

    /// <summary>
    /// Writes the entry for the current schedule (the database must be open). <paramref name="retryAfter"/>: start again after this wait at the
    /// latest (a claim or a run that failed). Errors are logged and returned in the record, never thrown.
    /// </summary>
    public async Task<TriggerRecord> ArmAsync(TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!current.StartWithSystem)
            {
                return await RemoveLockedAsync(cancellationToken).ConfigureAwait(false);
            }

            var now = clock.GetUtcNow();
            var nextPost = (await posts.NextScheduledAsync(cancellationToken).ConfigureAwait(false))?.ScheduledAtUtc;
            DateTimeOffset? next = null;
            if (!pause.IsPaused)
            {
                next = Earliest(nextPost, await processing.NextCheckUtcAsync(cancellationToken).ConfigureAwait(false));
                next = Earliest(next, retryAfter is { } wait ? now + wait : null);
            }

            if (next is { } at && at < now + TriggerLimits.SoonestStart)
            {
                next = now + TriggerLimits.SoonestStart;
            }

            if (ProgramFile() is not { } program)
            {
                logger.LogError("The program file could not be named, so the system scheduler cannot start it; posts are sent only while the window is open");
                return Save(new TriggerRecord { State = TriggerState.Failed, Kind = trigger.Kind, Name = Name, Message = "The program file could not be named.", UpdatedUtc = now });
            }

            var plan = new TriggerPlan(Name, program, TriggerNames.RunArguments(paths), next, Safety: next is not null, current.WakeToRun);
            var definition = trigger.Describe(plan);
            var last = Current;
            if (last.State == TriggerState.On && last.Definition == definition && await trigger.ExistsAsync(Name, cancellationToken).ConfigureAwait(false))
            {
                return last;
            }

            if (last.Name.Length > 0 && last.Name != Name)
            {
                // Written under another name before (another name prefix): that entry goes, so one data folder never has two.
                await trigger.RemoveAsync(last.Name, cancellationToken).ConfigureAwait(false);
            }

            var result = await trigger.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
            if (result.State != TriggerState.Failed)
            {
                logger.LogInformation("System scheduler ({Kind}) set: next start {Next}{Paused}", trigger.Kind,
                    next is { } n ? n.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture) : "only at sign-in",
                    pause.IsPaused ? " (paused)" : string.Empty);
            }

            return Save(new TriggerRecord
            {
                State = result.State,
                Kind = trigger.Kind,
                Name = Name,
                NextRunUtc = next,
                NextPostUtc = nextPost,
                Definition = result.State == TriggerState.Failed ? string.Empty : definition,
                Message = result.Message,
                UpdatedUtc = now,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The system scheduler's next start could not be set");
            return Save(new TriggerRecord { State = TriggerState.Failed, Kind = trigger.Kind, Name = Name, Message = ex.Message, UpdatedUtc = clock.GetUtcNow() });
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes the entry ("send while closed" turned off).</summary>
    public async Task<TriggerRecord> RemoveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RemoveLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a < b ? a : b;

    private async Task<TriggerRecord> RemoveLockedAsync(CancellationToken cancellationToken)
    {
        var last = Current;
        if (last.State == TriggerState.Off && File.Exists(RecordFile))
        {
            return last;
        }

        var result = await trigger.RemoveAsync(Name, cancellationToken).ConfigureAwait(false);
        if (result.State == TriggerState.Off)
        {
            logger.LogInformation("System scheduler ({Kind}) entry removed: posts are sent only while the window is open", trigger.Kind);
        }

        return Save(new TriggerRecord { State = result.State, Kind = trigger.Kind, Name = Name, Message = result.Message, UpdatedUtc = clock.GetUtcNow() });
    }

    private TriggerRecord Save(TriggerRecord record)
    {
        try
        {
            File.WriteAllText(RecordFile, JsonSerializer.Serialize(record, BackgroundJsonContext.Default.TriggerRecord));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("The record of the system scheduler entry could not be written ({Type}): {Reason}", ex.GetType().Name, ex.Message);
        }

        return record;
    }
}
