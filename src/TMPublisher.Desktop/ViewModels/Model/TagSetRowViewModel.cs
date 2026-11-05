using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Platforms;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>One platform's default tags.</summary>
public sealed partial class TagSetRowViewModel(IPlatform platform, string line) : ObservableObject
{
    /// <summary>Platform.</summary>
    public IPlatform Platform { get; } = platform;

    /// <summary>Tags on one line.</summary>
    [ObservableProperty]
    public partial string Line { get; set; } = line;
}
