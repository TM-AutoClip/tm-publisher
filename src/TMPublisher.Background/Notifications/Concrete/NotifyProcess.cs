using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Notifications;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;

namespace TMPublisher.Background.Notifications;

/// <summary>
/// Shows corner notifications of a run (no window is open): starts the program with <c>--notify</c> (it shows only the notification windows
/// and ends after the last one closes) and writes each notification as one JSON line to its standard input (never on the command line). The
/// same process takes the notifications that follow; its input is closed when the last one's time is over or when the run ends, and it ends
/// after the last notification closed. The run itself never loads a UI library.
/// </summary>
public sealed class NotifyProcess(AppPaths paths, TimeProvider clock, ILogger<NotifyProcess> logger) : IDisposable
{
    private static readonly TimeSpan ExtraTime = TimeSpan.FromSeconds(5);
    private readonly Lock _gate = new();
    private Process? _process;
    private StreamWriter? _input;
    private ITimer? _closer;
    private bool _noDisplayLogged;

    /// <summary>Shows the notification (starts the notification process when needed).</summary>
    public void Show(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                if (!_noDisplayLogged)
                {
                    logger.LogWarning("No graphical session (DISPLAY / WAYLAND_DISPLAY not set): notifications are only written to this log");
                    _noDisplayLogged = true;
                }

                return;
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (_input is null && !Start())
                {
                    return;
                }

                try
                {
                    _input!.WriteLine(NotificationLines.Write(message));
                    _input.Flush();
                    break;
                }
                catch (IOException ex)
                {
                    // The notification process ended between two notifications: a new one is started once.
                    logger.LogWarning("The notification process did not take the notification ({Reason}); starting a new one", ex.Message);
                    CloseInput();
                }
            }

            _closer?.Dispose();
            _closer = clock.CreateTimer(_ =>
            {
                lock (_gate)
                {
                    CloseInput();
                }
            }, null, TimeSpan.FromSeconds(message.Seconds) + ExtraTime, Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _closer?.Dispose();
            CloseInput();
        }
    }

    private bool Start()
    {
        if (ProgramFiles.Self is not { } window)
        {
            logger.LogError("The program file could not be named; the notification is only written to this log");
            return false;
        }

        var start = new ProcessStartInfo(window)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        start.ArgumentList.Add("--notify");
        if (!paths.IsDefaultDirectory)
        {
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(paths.DataDirectory);
        }

        try
        {
            _process = Process.Start(start) ?? throw new InvalidOperationException("The notification process did not start.");
            _input = _process.StandardInput;
            logger.LogInformation("Notification process started (process {ProcessId})", _process.Id);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            logger.LogError(ex, "The notification process could not be started");
            CloseInput();
            return false;
        }
    }

    private void CloseInput()
    {
        try
        {
            _input?.Dispose();
        }
        catch (IOException ex)
        {
            logger.LogWarning("Closing the notification process's input failed ({Reason})", ex.Message);
        }

        _input = null;
        _process?.Dispose();
        _process = null;
    }
}
