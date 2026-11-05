using TMPublisher.Core.Posts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A post on the calendar.</summary>
public sealed class CalendarItemViewModel(Post post, string time, string summary, string color, string state, bool canMove)
{
    /// <summary>Post.</summary>
    public Post Post { get; } = post;

    /// <summary>Local time ("14:30").</summary>
    public string Time { get; } = time;

    /// <summary>First words.</summary>
    public string Summary { get; } = summary;

    /// <summary>Color of the first target's platform (grey without targets).</summary>
    public string Color { get; } = color;

    /// <summary>State (localized).</summary>
    public string State { get; } = state;

    /// <summary>Can be dragged to another day.</summary>
    public bool CanMove { get; } = canMove;
}
