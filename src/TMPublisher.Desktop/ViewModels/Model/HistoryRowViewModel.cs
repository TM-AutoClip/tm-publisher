namespace TMPublisher.Desktop.ViewModels;

/// <summary>A post in the history.</summary>
public sealed class HistoryRowViewModel(string when, string summary, string state, IReadOnlyList<HistoryTargetViewModel> targets)
{
    /// <summary>Time (local).</summary>
    public string When { get; } = when;

    /// <summary>First words.</summary>
    public string Summary { get; } = summary;

    /// <summary>State.</summary>
    public string State { get; } = state;

    /// <summary>Targets.</summary>
    public IReadOnlyList<HistoryTargetViewModel> Targets { get; } = targets;
}
