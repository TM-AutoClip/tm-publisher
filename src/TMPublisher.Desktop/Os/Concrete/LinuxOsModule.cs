using Microsoft.Extensions.DependencyInjection;

namespace TMPublisher.Desktop.Os;

/// <summary>Linux: xdg-open for "show the file" (the key protector and the system scheduler come from the sending services).</summary>
public sealed class LinuxOsModule : IOsModule
{
    /// <inheritdoc />
    public bool IsCurrent => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public void Register(IServiceCollection services) => services.AddSingleton<IFileReveal, LinuxFileReveal>();
}
