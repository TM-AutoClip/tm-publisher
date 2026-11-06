using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Desktop.Os;

/// <summary>
/// Operating system services of the window (showing a file in the file manager). The modules come from a list and the current one registers itself:
/// no "if Windows" branch in the rest of the code.
/// </summary>
public interface IOsModule
{
    /// <summary>Whether this module is for the running operating system.</summary>
    bool IsCurrent { get; }

    /// <summary>Registers the services.</summary>
    void Register(IServiceCollection services);
}
