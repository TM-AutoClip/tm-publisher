using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Triggers;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Os;

/// <summary>Windows: DPAPI key protection, a Task Scheduler task as the system scheduler, the Run-key entry of 0.1.0–0.1.4 as a leftover.</summary>
public sealed class WindowsBackgroundOs : IBackgroundOs
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<ISystemTrigger, WindowsTaskTrigger>();
        services.AddSingleton<ILegacyAutostart, WindowsLegacyAutostart>();
        if (OperatingSystem.IsWindows())
        {
            AddKeyProtector(services);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AddKeyProtector(IServiceCollection services) => services.AddSingleton<IKeyProtector, DpapiKeyProtector>();
}
