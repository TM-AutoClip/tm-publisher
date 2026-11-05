using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Notifications;
using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Desktop.Notifications;

/// <summary>
/// The window program started with <c>--notify</c> by the background scheduler when no window is open: no database, no main window, only the
/// corner notifications. Each line of standard input is one notification (JSON); when the scheduler closes the input and the last notification
/// has closed, the process ends. Its memory is used only while something is on screen.
/// </summary>
public sealed class NotifyMode(ToastStack stack, ILogger<NotifyMode> logger)
{
    private static readonly TimeSpan NothingArrived = TimeSpan.FromSeconds(30);
    private volatile bool _inputEnded;
    private int _shown;

    /// <summary>Starts reading the notifications (on the UI thread, after Avalonia started).</summary>
    public void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        stack.Emptied += (_, _) =>
        {
            if (_inputEnded)
            {
                logger.LogInformation("Last notification closed; ending");
                desktop.Shutdown();
            }
        };
        new Thread(() => Read(desktop)) { IsBackground = true, Name = "TM Publisher notification input" }.Start();
        DispatcherTimer.RunOnce(() =>
        {
            if (Volatile.Read(ref _shown) == 0)
            {
                logger.LogWarning("No notification arrived within {Wait}; ending", NothingArrived);
                desktop.Shutdown();
            }
        }, NothingArrived);
    }

    private void Read(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            while (reader.ReadLine() is { } line)
            {
                if (SchedulerChannel.ParseNotification(line) is not { } message)
                {
                    logger.LogWarning("An input line was not a notification; ignored");
                    continue;
                }

                Interlocked.Increment(ref _shown);
                stack.Show(new AppNotification(message.Kind, message.Title, message.Message), TimeSpan.FromSeconds(Math.Max(3, message.Seconds)));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogError(ex, "Reading the notifications from the scheduler failed");
        }

        _inputEnded = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (stack.Open.Count == 0)
            {
                logger.LogInformation("Input closed and nothing on screen; ending");
                desktop.Shutdown();
            }
        });
    }
}
