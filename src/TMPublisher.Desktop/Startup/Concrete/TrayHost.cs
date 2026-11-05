using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.Startup;

/// <summary>Tray icon with Open and Quit. Some Linux desktops have no tray host (GNOME without an extension): the window then minimizes.</summary>
public sealed class TrayHost(Localizer text, ILogger<TrayHost> logger)
{
    private TrayIcon? _icon;

    /// <summary>Creates the icon.</summary>
    public void Create(Action open, Action quit)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://TMPublisher/Assets/app-256.png"));
            var openItem = new NativeMenuItem(text["tray.open"]);
            openItem.Click += (_, _) => open();
            var quitItem = new NativeMenuItem(text["tray.quit"]);
            quitItem.Click += (_, _) => quit();
            _icon = new TrayIcon
            {
                Icon = new WindowIcon(stream),
                ToolTipText = "TM Publisher",
                Menu = [openItem, new NativeMenuItemSeparator(), quitItem],
                IsVisible = true,
            };
            _icon.Clicked += (_, _) => open();
            TrayIcon.SetIcons(Application.Current!, [_icon]);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            logger.LogWarning(ex, "Tray icon could not be created; the window will minimize instead of hiding");
        }
    }

    /// <summary>Whether the icon exists.</summary>
    public bool HasIcon => _icon is not null;
}
