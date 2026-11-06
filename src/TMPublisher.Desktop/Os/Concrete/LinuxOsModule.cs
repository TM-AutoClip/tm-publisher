using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// Linux: Secret Service key protection (secret-tool), XDG autostart of the background scheduler, xdg-open for "show the file".
/// </summary>
public sealed class LinuxOsModule : IOsModule
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IKeyProtector, SecretServiceKeyProtector>();
        services.AddSingleton<IAutostart, LinuxAutostart>();
        services.AddSingleton<IFileReveal, LinuxFileReveal>();
    }
}
