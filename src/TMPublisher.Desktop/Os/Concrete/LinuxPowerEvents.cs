using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;
using TMPublisher.Scheduling;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// Linux: logind's <c>PrepareForSleep(false)</c> signal on the system bus (sent when the computer wakes up). Without logind or the system
/// bus (some containers) the scheduler still wakes at least once a day.
/// </summary>
public sealed class LinuxPowerEvents(ILogger<LinuxPowerEvents> logger) : IPowerEvents, IDisposable
{
    private DBusConnection? _connection;
    private IDisposable? _watch;

    /// <inheritdoc />
    public event EventHandler? ScheduleStale;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null || DBusAddress.System is not { Length: > 0 } address)
        {
            return;
        }

        try
        {
            _connection = new DBusConnection(address);
            await _connection.ConnectAsync().ConfigureAwait(false);
            _watch = await _connection.WatchSignalAsync("org.freedesktop.login1", "/org/freedesktop/login1", "org.freedesktop.login1.Manager", "PrepareForSleep",
                static (Message message, object? _) => message.GetBodyReader().ReadBool(), OnPrepareForSleep, ObserverFlags.None, false, null).ConfigureAwait(false);
            logger.LogInformation("Listening for resume through logind");
        }
        catch (Exception ex) when (ex is DBusConnectionException or DBusErrorReplyException or IOException or System.Net.Sockets.SocketException or UnauthorizedAccessException)
        {
            // No system bus or no logind here: logged with the reason; the daily wake-up still re-reads the schedule.
            logger.LogWarning("Resume events are not available ({Type}: {Reason}); the schedule is re-read at least daily", ex.GetType().Name, ex.Message);
            Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _watch?.Dispose();
        _watch = null;
        _connection?.Dispose();
        _connection = null;
    }

    private void OnPrepareForSleep(Notification<bool> signal)
    {
        if (signal.Exception is { } error)
        {
            logger.LogWarning("The logind watch stopped ({Type}: {Reason})", error.GetType().Name, error.Message);
            return;
        }

        if (signal.HasValue && !signal.Value)
        {
            logger.LogInformation("The computer woke up; checking the schedule");
            ScheduleStale?.Invoke(this, EventArgs.Empty);
        }
    }
}
