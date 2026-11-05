using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Posts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A post that waits for the user's decision.</summary>
public sealed partial class AttentionRowViewModel(Post post, string summary, string when, string state, string targets) : ObservableObject
{
    /// <summary>Post.</summary>
    public Post Post { get; } = post;

    /// <summary>First words of the post.</summary>
    public string Summary { get; } = summary;

    /// <summary>Planned time (local).</summary>
    public string When { get; } = when;

    /// <summary>Why it waits.</summary>
    public string State { get; } = state;

    /// <summary>One line per target with its state and message.</summary>
    public string Targets { get; } = targets;

    /// <summary>Stopped in the middle (retry / skip are offered).</summary>
    public bool IsStopped => Post.Status == PostStatus.Stopped;

    /// <summary>New date.</summary>
    [ObservableProperty]
    public partial DateTimeOffset? Date { get; set; }

    /// <summary>New time.</summary>
    [ObservableProperty]
    public partial TimeSpan? Time { get; set; }
}
