using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Desktop.Os;

/// <summary>Windows: Explorer for "show the file" (the key protector and the system scheduler come from the sending services).</summary>
public sealed class WindowsOsModule : IOsModule
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public void Register(IServiceCollection services) => services.AddSingleton<IFileReveal, WindowsFileReveal>();
}
