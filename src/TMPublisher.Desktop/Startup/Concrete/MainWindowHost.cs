using System.Runtime;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Logging;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// The main window's life. It is created when it is first shown (a start in the tray makes none). When the user closes it and a tray icon
/// is there, it is really closed and released instead of hidden: its views, bindings, fonts in use and drawing surfaces go, so the app spends
/// the day in the tray with little more than its scheduler. The next "Open" makes a new window over the same view models (singletons), so
/// an unfinished post and the shown page are there again. Without a reliable tray the window minimizes (it is the only way back).
/// Notifications are their own small windows and do not need this one.
/// </summary>
public sealed class MainWindowHost(WindowHost windows, ILogger<MainWindowHost> logger)
{
    private Func<Window>? _create;
    private Func<bool> _canRelease = static () => false;
    private Action<Window?> _setMain = static _ => { };

    /// <summary>The open main window; <c>null</c> before the first show and after it was released to the tray.</summary>
    public Window? Window { get; private set; }

    /// <summary>The app is ending (Quit, operating system shutdown): closing is not refused and nothing is released.</summary>
    public bool Quitting { get; private set; }

    /// <summary>A window was closed and released to the tray (raised after the memory is given back).</summary>
    public event EventHandler? Released;

    /// <summary>
    /// Sets how a window is made (<paramref name="create"/>), whether closing may release it (<paramref name="canRelease"/>: a tray icon is
    /// there to bring it back) and who learns the current main window (<paramref name="setMain"/>: the desktop lifetime).
    /// </summary>
    public void Configure(Func<Window> create, Func<bool> canRelease, Action<Window?> setMain)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _canRelease = canRelease ?? throw new ArgumentNullException(nameof(canRelease));
        _setMain = setMain ?? throw new ArgumentNullException(nameof(setMain));
    }

    /// <summary>Shows the window, making a new one if there is none, and brings it to the front.</summary>
    public Window Show()
    {
        if (Window is null)
        {
            var window = (_create ?? throw new InvalidOperationException("Configure the main window first."))();
            window.Closing += OnClosing;
            window.Closed += OnClosed;
            Window = window;
            windows.Active = window;
            _setMain(window);
        }

        Window.Show();
        if (Window.WindowState == WindowState.Minimized)
        {
            Window.WindowState = WindowState.Normal;
        }

        Window.Activate();
        return Window;
    }

    /// <summary>The app is ending: the next close is not refused.</summary>
    public void BeginQuit() => Quitting = true;

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Quitting)
        {
            return;
        }

        if (e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
        {
            // Windows logoff / restart / update, or the app itself ending: never refuse it ("TM Publisher is preventing shutdown").
            Quitting = true;
            logger.LogInformation("Closing for {Reason}", e.CloseReason);
            return;
        }

        if (_canRelease())
        {
            // Closed and released in OnClosed; the tray icon brings a new window.
            return;
        }

        e.Cancel = true;
        ((Window)sender!).WindowState = WindowState.Minimized;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.Closing -= OnClosing;
        window.Closed -= OnClosed;

        // Nothing may keep the closed window alive: not the dialog owner, not the lifetime, not the bindings to the singleton view models.
        if (Window == window)
        {
            Window = null;
        }

        if (windows.Active == window)
        {
            windows.Active = null;
        }

        _setMain(null);
        window.DataContext = null;
        if (Quitting)
        {
            return;
        }

        // After the close has finished (the visual tree and the drawing surface are gone), give the freed memory back to the system once:
        // a compacting full collection (large object heap included) lets the runtime return the emptied segments. Not repeated, no timer.
        Dispatcher.UIThread.Post(() =>
        {
            Compact();
            MemoryUse.LogTrayEntry(logger, "window closed");
            Released?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Background);
    }

    /// <summary>One blocking, compacting collection of every generation, the large object heap too.</summary>
    internal static void Compact()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // Native drawing objects (Skia) of the closed window are freed by finalizers; collect what they released.
        GC.WaitForPendingFinalizers();
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
