using Avalonia.Controls;
using Avalonia.Input.Platform;
using TMPublisher.Desktop.ViewModels;

namespace TMPublisher.Desktop.Views;

/// <summary>"Report a problem" dialog.</summary>
public sealed partial class ReportProblemWindow : Window
{
    /// <summary>Creates.</summary>
    public ReportProblemWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ReportProblemViewModel model)
            {
                model.CopyToClipboard = async value =>
                {
                    if (Clipboard is { } clipboard)
                    {
                        await clipboard.SetTextAsync(value).ConfigureAwait(true);
                    }
                };
            }
        };
    }
}
