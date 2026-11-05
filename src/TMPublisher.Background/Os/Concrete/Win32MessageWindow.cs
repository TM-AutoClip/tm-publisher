using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Background.Os;

/// <summary>
/// A hidden top-level window and its message loop on the main thread (never shown). Top-level windows receive the broadcasts the scheduler
/// needs: waking from sleep (<c>WM_POWERBROADCAST</c>), clock and time-zone changes (<c>WM_TIMECHANGE</c>), log off / shut down
/// (<c>WM_ENDSESSION</c>), Explorer restarting (<c>TaskbarCreated</c>, the tray icon is added again), and the tray icon's mouse messages.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class Win32MessageWindow(ILogger<Win32MessageWindow> logger) : IMainLoop
{
    /// <summary>The tray icon's callback message.</summary>
    public const uint TrayCallback = Win32Native.WmApp + 1;

    private static Win32MessageWindow? _current;
    private nint _window;
    private uint _taskbarCreated;
    private volatile bool _quit;

    /// <inheritdoc />
    public event EventHandler? StopRequested;

    /// <summary>The window exists (the tray icon can be added).</summary>
    public event EventHandler? Created;

    /// <summary>The computer woke up.</summary>
    public event EventHandler? Resumed;

    /// <summary>The clock or the time zone changed.</summary>
    public event EventHandler? TimeChanged;

    /// <summary>Explorer started again (the tray icon must be added again).</summary>
    public event EventHandler? TaskbarCreated;

    /// <summary>A mouse message of the tray icon (<c>WM_LBUTTONUP</c>, <c>WM_RBUTTONUP</c>...).</summary>
    public event EventHandler<uint>? TrayMessage;

    /// <summary>The window handle (0 before <see cref="Run"/>).</summary>
    public nint Handle => _window;

    /// <inheritdoc />
    public void Run()
    {
        _current = this;
        var instance = Win32Native.GetModuleHandle(0);
        fixed (char* name = "TMPublisherSchedulerWindow")
        fixed (char* title = "TM Publisher scheduler")
        {
            var windowClass = new Win32WindowClass
            {
                Size = (uint)sizeof(Win32WindowClass),
                WindowProc = &WindowProc,
                Instance = instance,
                ClassName = name,
            };
            if (Win32Native.RegisterClass(&windowClass) == 0)
            {
                logger.LogError("The hidden window class could not be registered (Win32 error {Error}); no tray icon, no resume events", Marshal.GetLastPInvokeError());
            }

            _window = Win32Native.CreateWindow(0, name, title, 0, 0, 0, 0, 0, 0, 0, instance, 0);
        }

        if (_window == 0)
        {
            logger.LogError("The hidden window could not be created (Win32 error {Error}); no tray icon, no resume events", Marshal.GetLastPInvokeError());
            WaitWithoutWindow();
            return;
        }

        _taskbarCreated = Win32Native.RegisterWindowMessage("TaskbarCreated");
        Raise(Created, nameof(Created));
        if (_quit)
        {
            Win32Native.PostMessage(_window, Win32Native.WmClose, 0, 0);
        }

        Win32Message message;
        while (Win32Native.GetMessage(&message, 0, 0, 0) > 0)
        {
            Win32Native.TranslateMessage(&message);
            Win32Native.DispatchMessage(&message);
        }

        logger.LogInformation("Message loop ended");
    }

    /// <inheritdoc />
    public void Quit()
    {
        _quit = true;
        if (_window != 0 && Win32Native.PostMessage(_window, Win32Native.WmClose, 0, 0) == 0)
        {
            logger.LogError("Could not ask the hidden window to close (Win32 error {Error})", Marshal.GetLastPInvokeError());
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(nint window, uint message, nint wParam, nint lParam) =>
        _current is { } current ? current.OnMessage(window, message, wParam, lParam) : Win32Native.DefaultWindowProc(window, message, wParam, lParam);

    private nint OnMessage(nint window, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case Win32Native.WmPowerBroadcast:
                if (wParam == Win32Native.PowerResumeAutomatic)
                {
                    Raise(Resumed, nameof(Resumed));
                }

                return 1;
            case Win32Native.WmTimeChange:
                TimeZoneInfo.ClearCachedData();
                Raise(TimeChanged, nameof(TimeChanged));
                return 0;
            case Win32Native.WmQueryEndSession:
                return 1;
            case Win32Native.WmEndSession:
                if (wParam != 0)
                {
                    logger.LogInformation("Windows is logging off or shutting down");
                    Raise(StopRequested, nameof(StopRequested));
                }

                return 0;
            case TrayCallback:
                try
                {
                    TrayMessage?.Invoke(this, (uint)(lParam & 0xFFFF));
                }
                catch (Exception ex)
                {
                    // An exception must not leave a native callback (the process would end): logged with its reason.
                    logger.LogError(ex, "A tray icon action failed");
                }

                return 0;
            case Win32Native.WmClose:
                Win32Native.DestroyWindow(window);
                return 0;
            case Win32Native.WmDestroy:
                Win32Native.PostQuitMessage(0);
                return 0;
            default:
                if (message == _taskbarCreated && _taskbarCreated != 0)
                {
                    Raise(TaskbarCreated, nameof(TaskbarCreated));
                    return 0;
                }

                return Win32Native.DefaultWindowProc(window, message, wParam, lParam);
        }
    }

    private void Raise(EventHandler? handler, string name)
    {
        try
        {
            handler?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // An exception must not leave a native callback (the process would end): logged with its reason.
            logger.LogError(ex, "Handling the window event {Event} failed", name);
        }
    }

    private void WaitWithoutWindow()
    {
        while (!_quit)
        {
            Thread.Sleep(1000);
        }
    }
}
