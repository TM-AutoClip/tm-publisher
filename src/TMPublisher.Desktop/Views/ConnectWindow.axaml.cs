using Avalonia.Controls;
using Avalonia.Input.Platform;
using TMPublisher.Desktop.ViewModels;

namespace TMPublisher.Desktop.Views;

/// <summary>Connection window (one window for everything).</summary>
public sealed partial class ConnectWindow : Window
{
    /// <summary>Creates.</summary>
    public ConnectWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectViewModel model)
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
