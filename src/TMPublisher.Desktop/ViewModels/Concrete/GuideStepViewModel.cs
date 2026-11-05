using CommunityToolkit.Mvvm.ComponentModel;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>A checkable step of a setup guide (ticked by the user, like a test list).</summary>
public sealed partial class GuideStepViewModel(int number, string text, Uri? link, Func<string>? copyValue) : ObservableObject
{
    /// <summary>Step number.</summary>
    public int Number { get; } = number;

    /// <summary>Text.</summary>
    public string Text { get; } = text;

    /// <summary>Page to open.</summary>
    public Uri? Link { get; } = link;

    /// <summary>Whether there is a page.</summary>
    public bool HasLink => Link is not null;

    /// <summary>Whether there is a value to copy.</summary>
    public bool HasCopy => copyValue is not null;

    /// <summary>The value to copy now.</summary>
    public string CopyValue => copyValue?.Invoke() ?? string.Empty;

    /// <summary>Ticked.</summary>
    [ObservableProperty]
    public partial bool IsDone { get; set; }
}
