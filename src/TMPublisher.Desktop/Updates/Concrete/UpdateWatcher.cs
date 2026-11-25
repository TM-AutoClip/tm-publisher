using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Startup;

namespace TMPublisher.Desktop.Updates;

/// <summary>
/// The window's look for a new version: at every start (when it is on in Settings) in the background, after the main window is shown, so the
/// start never waits for GitHub; and on "Check now" in Settings. The last result is kept here and <see cref="Changed"/> tells the window (on a
/// background thread). Only the window looks; the short runs the system scheduler starts never do.
/// </summary>
public sealed class UpdateWatcher(ReleaseChecker checker, UiPreferencesFile preferences, ILogger<UpdateWatcher> logger) : IStartupTask, IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>The last result (<see cref="UpdateCheckResult.None"/> before the first look).</summary>
    public UpdateCheckResult Last { get; private set; } = UpdateCheckResult.None;

    /// <summary>The look in the background that the start began (tests wait for it); a finished task when none was started.</summary>
    public Task Running { get; private set; } = Task.CompletedTask;

    /// <summary>A look finished (raised on the thread that looked).</summary>
    public event EventHandler<UpdateCheckResult>? Changed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!preferences.Read().CheckForUpdates)
        {
            Last = new UpdateCheckResult(UpdateOutcome.Off, null, UpdateRules.DownloadPage);
            logger.LogInformation("Update check at start is off");
            return Task.CompletedTask;
        }

        // Not awaited: the start goes on while GitHub answers.
        Running = Task.Run(() => CheckAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Looks now (one look at a time: a second call waits for the first and looks again).</summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await checker.CheckAsync(cancellationToken).ConfigureAwait(false);
            Last = result;
            Changed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Last;
        }
        catch (Exception ex)
        {
            // A look must never end the window: anything unexpected is logged and the last result stays.
            logger.LogError(ex, "Update check failed unexpectedly ({Type})", ex.GetType().Name);
            return Last;
        }
        finally
        {
            _one.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _one.Dispose();
}
