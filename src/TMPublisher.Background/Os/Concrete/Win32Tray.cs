using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Localization;
using TMPublisher.Runtime.Ipc;

namespace TMPublisher.Background.Os;

/// <summary>
/// The tray icon with plain <c>Shell_NotifyIcon</c> calls (no UI library): click opens the window, right click shows Open · Pause / Resume
/// scheduling · Quit. The tooltip tells what the scheduler does. When Explorer restarts the icon is added again.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class Win32Tray : ITrayIcon
{
    private const uint IconId = 1;
    private const nuint OpenCommand = 1;
    private const nuint PauseCommand = 2;
    private const nuint QuitCommand = 3;

    private readonly Win32MessageWindow _window;
    private readonly ILocalizer _text;
    private readonly ILogger<Win32Tray> _logger;
    private readonly Lock _gate = new();
    private nint _icon;
    private bool _added;
    private bool _paused;
    private string _tip = "TM Publisher";

    /// <summary>Creates; the icon is added when the hidden window exists.</summary>
    public Win32Tray(Win32MessageWindow window, ILocalizer text, ILogger<Win32Tray> logger)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _text = text;
        _logger = logger;
        window.Created += (_, _) => Add();
        window.TaskbarCreated += (_, _) =>
        {
            _added = false;
            Add();
        };
        window.TrayMessage += (_, mouse) => OnMouse(mouse);
    }

    /// <inheritdoc />
    public event EventHandler? OpenRequested;

    /// <inheritdoc />
    public event EventHandler? PauseToggled;

    /// <inheritdoc />
    public event EventHandler? QuitRequested;

    /// <inheritdoc />
    public void Update(SchedulerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            _paused = state.Status == SchedulerStatus.Paused;
            var line = _text["scheduler.state." + state.Status.ToString().ToLowerInvariant()];
            _tip = "TM Publisher – " + line;
            if (_added)
            {
                Send(Win32Native.NotifyModify, Win32Native.NotifyFlagTip);
            }
        }
    }

    private void Add()
    {
        lock (_gate)
        {
            if (_icon == 0)
            {
                nint small = 0;
                if (Environment.ProcessPath is { } exe && Win32Native.ExtractIcons(exe, 0, null, &small, 1) > 0 && small != 0)
                {
                    _icon = small;
                }
                else
                {
                    _logger.LogWarning("The program icon could not be read; the standard icon is used in the tray");
                    _icon = Win32Native.LoadIcon(0, 32512);
                }
            }

            _added = Send(Win32Native.NotifyAdd, Win32Native.NotifyFlagMessage | Win32Native.NotifyFlagIcon | Win32Native.NotifyFlagTip);
            if (_added)
            {
                _logger.LogInformation("Tray icon added");
            }
        }
    }

    private bool Send(uint message, uint flags)
    {
        var data = new Win32NotifyIconData
        {
            Size = (uint)sizeof(Win32NotifyIconData),
            Window = _window.Handle,
            Id = IconId,
            Flags = flags,
            CallbackMessage = Win32MessageWindow.TrayCallback,
            Icon = _icon,
        };
        var tip = _tip.Length > 127 ? _tip[..127] : _tip;
        for (var i = 0; i < tip.Length; i++)
        {
            data.Tip[i] = tip[i];
        }

        if (Win32Native.ShellNotifyIcon(message, &data) != 0)
        {
            return true;
        }

        // Explorer may not be running yet at sign-in; TaskbarCreated adds the icon when it starts.
        _logger.LogWarning("The tray icon could not be {Action} (Win32 error {Error})", message == Win32Native.NotifyAdd ? "added" : "changed", Marshal.GetLastPInvokeError());
        return false;
    }

    private void OnMouse(uint mouse)
    {
        switch (mouse)
        {
            case Win32Native.WmLeftButtonUp:
            case Win32Native.WmLeftButtonDoubleClick:
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Win32Native.WmRightButtonUp:
            case Win32Native.WmContextMenu:
                ShowMenu();
                break;
        }
    }

    private void ShowMenu()
    {
        var menu = Win32Native.CreatePopupMenu();
        if (menu == 0)
        {
            _logger.LogError("The tray menu could not be made (Win32 error {Error})", Marshal.GetLastPInvokeError());
            return;
        }

        try
        {
            Win32Native.AppendMenu(menu, Win32Native.MenuString, OpenCommand, _text["tray.open"]);
            Win32Native.AppendMenu(menu, Win32Native.MenuString, PauseCommand, _text[_paused ? "tray.resume" : "tray.pause"]);
            Win32Native.AppendMenu(menu, Win32Native.MenuSeparator, 0, null);
            Win32Native.AppendMenu(menu, Win32Native.MenuString, QuitCommand, _text["tray.quit"]);
            Win32Point point;
            Win32Native.GetCursorPos(&point);

            // The menu closes when clicked elsewhere only if the window is in the foreground (documented TrackPopupMenu behaviour).
            Win32Native.SetForegroundWindow(_window.Handle);
            var command = (nuint)Win32Native.TrackPopupMenu(menu, Win32Native.TrackRightButton | Win32Native.TrackReturnCommand | Win32Native.TrackBottomAlign,
                point.X, point.Y, 0, _window.Handle, 0);
            Win32Native.PostMessage(_window.Handle, 0, 0, 0);
            switch (command)
            {
                case OpenCommand:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case PauseCommand:
                    PauseToggled?.Invoke(this, EventArgs.Empty);
                    break;
                case QuitCommand:
                    QuitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            Win32Native.DestroyMenu(menu);
        }
    }

    /// <inheritdoc />
    public void Remove()
    {
        lock (_gate)
        {
            if (_added)
            {
                Send(Win32Native.NotifyDelete, 0);
                _added = false;
            }
        }
    }
}
