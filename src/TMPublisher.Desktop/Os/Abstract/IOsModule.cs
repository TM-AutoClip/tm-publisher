using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// Operating system services (key protector, autostart, power events). The modules come from a list and the current one registers itself:
/// no "if Windows" branch in the rest of the code.
/// </summary>
public interface IOsModule
{
    /// <summary>Whether this module is for the running operating system.</summary>
    bool IsCurrent { get; }

    /// <summary>Whether a hidden window can always be brought back from the tray (false on Linux desktops without a tray host).</summary>
    bool TrayIsReliable { get; }

    /// <summary>Registers the services.</summary>
    void Register(IServiceCollection services);
}
