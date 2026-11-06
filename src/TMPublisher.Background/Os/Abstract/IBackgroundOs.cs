using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Background.Os;

/// <summary>
/// Operating system services of sending (key protector, system scheduler, leftovers of older versions). The modules come from a list and the
/// current one registers itself: no "if Windows" branch in the rest of the code.
/// </summary>
public interface IBackgroundOs
{
    /// <summary>Whether this module is for the running operating system.</summary>
    bool IsCurrent { get; }

    /// <summary>Registers the services.</summary>
    void Register(IServiceCollection services);
}
