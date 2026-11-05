using TMPublisher.Core.Platforms;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A platform in a drop-down list.</summary>
public sealed record PlatformOption(IPlatform Platform, string Name);
