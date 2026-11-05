using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TMPublisher.Scheduling;

namespace TMPublisher.Desktop.Os;

/// <summary>Windows: resume from sleep (<c>PowerModeChanged</c>) and clock / time-zone changes (<c>TimeChanged</c>).</summary>
public sealed class WindowsPowerEvents(ILogger<WindowsPowerEvents> logger) : IPowerEvents, IDisposable
{
    private bool _listening;

    /// <inheritdoc />
    public event EventHandler? ScheduleStale;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || _listening)
        {
            return Task.CompletedTask;
        }

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        _listening = true;
        logger.LogInformation("Listening for resume and clock changes");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_listening && OperatingSystem.IsWindows())
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            _listening = false;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (OperatingSystem.IsWindows() && e.Mode == PowerModes.Resume)
        {
            logger.LogInformation("The computer woke up; checking the schedule");
            ScheduleStale?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        logger.LogInformation("The clock changed; checking the schedule");
        ScheduleStale?.Invoke(this, EventArgs.Empty);
    }
}
