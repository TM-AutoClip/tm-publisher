using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Os;

/// <summary>Windows: DPAPI key protection, a hidden message window as the main loop, its tray icon and its resume / clock messages.</summary>
public sealed class WindowsBackgroundOs : IBackgroundOs
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            Add(services);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Add(IServiceCollection services)
    {
        services.AddSingleton<IKeyProtector, DpapiKeyProtector>();
        services.AddSingleton<Win32MessageWindow>();
        services.AddSingleton<IMainLoop>(s => s.GetRequiredService<Win32MessageWindow>());
        services.AddSingleton<ITrayIcon, Win32Tray>();
        services.AddSingleton<IPowerEvents, Win32PowerEvents>();
    }
}
