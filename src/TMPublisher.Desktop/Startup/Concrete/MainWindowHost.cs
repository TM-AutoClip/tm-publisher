using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace TMPublisher.Desktop.Startup;

/// <summary>
/// The main window's life. The window program runs only while the window is open (and, after it closed, until a post being sent is
/// finished): closing the window ends it, and the system scheduler starts a short run when the next post is due. A page with unsaved input refuses the first close
/// once (<see cref="Configure"/>'s <c>canClose</c>). Logging off or shutting down is never refused.
/// </summary>
public sealed class MainWindowHost(WindowHost windows, ILogger<MainWindowHost> logger)
{
    private Func<Window>? _create;
    private Func<bool> _canClose = static () => true;
    private Action<Window?> _setMain = static _ => { };

    /// <summary>The open main window; <c>null</c> before the first show and after it closed.</summary>
    public Window? Window { get; private set; }

    /// <summary>The app is ending (Quit, operating system shutdown): closing is not refused.</summary>
    public bool Quitting { get; private set; }

    /// <summary>The window closed because the system shuts down, restarts or signs out (a post being sent then gets only a short wait).</summary>
    public bool ClosedForShutdown { get; private set; }

    /// <summary>The window closed (the window program ends).</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Sets how a window is made (<paramref name="create"/>), whether the user may close it now (<paramref name="canClose"/>: no unsaved input,
    /// or the user closed again after the note) and who learns the current main window (<paramref name="setMain"/>: the desktop lifetime).
    /// </summary>
    public void Configure(Func<Window> create, Func<bool> canClose, Action<Window?> setMain)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _canClose = canClose ?? throw new ArgumentNullException(nameof(canClose));
        _setMain = setMain ?? throw new ArgumentNullException(nameof(setMain));
    }

    /// <summary>Shows the window, making it if there is none, and brings it to the front.</summary>
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
        if (e.CloseReason == WindowCloseReason.OSShutdown)
        {
            ClosedForShutdown = true;
        }

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

        if (!_canClose())
        {
            e.Cancel = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.Closing -= OnClosing;
        window.Closed -= OnClosed;
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
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
