using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Posts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>One target of a post in the history.</summary>
public sealed partial class HistoryTargetViewModel(PostTarget target, string title, string state) : ObservableObject
{
    /// <summary>Target.</summary>
    public PostTarget Target { get; } = target;

    /// <summary>"Platform · account".</summary>
    public string Title { get; } = title;

    /// <summary>State (localized) and message.</summary>
    public string State { get; } = state;

    /// <summary>Address of the published item.</summary>
    public string? Url => Target.Url;

    /// <summary>Has an address.</summary>
    public bool HasUrl => !string.IsNullOrEmpty(Target.Url);

    /// <summary>Counters after "Fetch".</summary>
    [ObservableProperty]
    public partial string Stats { get; set; } = string.Empty;
}
