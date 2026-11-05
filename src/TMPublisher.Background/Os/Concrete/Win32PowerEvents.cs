using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using TMPublisher.Scheduling;

namespace TMPublisher.Background.Os;

/// <summary>Windows: resume from sleep and clock / time-zone changes, from the hidden window's messages.</summary>
[SupportedOSPlatform("windows")]
public sealed class Win32PowerEvents : IPowerEvents
{
    private readonly ILogger<Win32PowerEvents> _logger;

    /// <summary>Creates.</summary>
    public Win32PowerEvents(Win32MessageWindow window, ILogger<Win32PowerEvents> logger)
    {
        ArgumentNullException.ThrowIfNull(window);
        _logger = logger;
        window.Resumed += (_, _) =>
        {
            _logger.LogInformation("The computer woke up; checking the schedule once the network is back");
            Resumed?.Invoke(this, EventArgs.Empty);
        };
        window.TimeChanged += (_, _) =>
        {
            _logger.LogInformation("The clock changed; checking the schedule");
            ScheduleStale?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <inheritdoc />
    public event EventHandler? ScheduleStale;

    /// <inheritdoc />
    public event EventHandler? Resumed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listening for resume and clock changes (window messages)");
        return Task.CompletedTask;
    }
}
