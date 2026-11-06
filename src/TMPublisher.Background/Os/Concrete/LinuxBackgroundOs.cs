using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Triggers;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Os;

/// <summary>
/// Linux: Secret Service key protection (secret-tool), a systemd user timer as the system scheduler (an XDG autostart entry without a systemd
/// user session), the XDG autostart entry of 0.1.0–0.1.4 as a leftover.
/// </summary>
public sealed class LinuxBackgroundOs : IBackgroundOs
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IKeyProtector, SecretServiceKeyProtector>();
        services.AddSingleton<XdgAutostartEntry>();
        services.AddSingleton<ISystemTrigger, LinuxSystemTrigger>();
        services.AddSingleton<ILegacyAutostart, LinuxLegacyAutostart>();
    }
}
