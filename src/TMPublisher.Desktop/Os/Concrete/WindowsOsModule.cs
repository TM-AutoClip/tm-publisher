using Microsoft.Extensions.DependencyInjection;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Desktop.Os;

/// <summary>Windows: DPAPI key protection, Run-key autostart of the background scheduler.</summary>
public sealed class WindowsOsModule : IOsModule
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IKeyProtector, DpapiKeyProtector>();
        services.AddSingleton<IAutostart, WindowsAutostart>();
    }
}
