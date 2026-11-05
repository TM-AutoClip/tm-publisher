using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Background.Os;

/// <summary>
/// Operating system services of the background scheduler (key protector, main loop, tray, resume and clock events). The modules come from a
/// list and the current one registers itself: no "if Windows" branch in the rest of the code.
/// </summary>
public interface IBackgroundOs
{
    /// <summary>Whether this module is for the running operating system.</summary>
    bool IsCurrent { get; }

    /// <summary>Registers the services.</summary>
    void Register(IServiceCollection services);
}
