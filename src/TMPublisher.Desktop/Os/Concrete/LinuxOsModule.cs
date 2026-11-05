using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// Linux: Secret Service key protection (secret-tool), XDG autostart. The tray needs a StatusNotifierItem host (GNOME needs an extension),
/// so closing the window minimizes it instead of hiding it.
/// </summary>
public sealed class LinuxOsModule : IOsModule
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public bool TrayIsReliable => false;

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IKeyProtector, SecretServiceKeyProtector>();
        services.AddSingleton<IAutostart, LinuxAutostart>();
        services.AddSingleton<TMPublisher.Scheduling.IPowerEvents, LinuxPowerEvents>();
    }
}
