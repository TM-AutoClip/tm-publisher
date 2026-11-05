using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Os;

/// <summary>Linux: Secret Service key protection (secret-tool), signals as the main loop, no tray, logind resume and timerfd clock events.</summary>
public sealed class LinuxBackgroundOs : IBackgroundOs
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IKeyProtector, SecretServiceKeyProtector>();
        services.AddSingleton<IMainLoop, SignalMainLoop>();
        services.AddSingleton<ITrayIcon, NoTrayIcon>();
        services.AddSingleton<LinuxClockChanges>();
        services.AddSingleton<IPowerEvents, LinuxPowerEvents>();
    }
}
