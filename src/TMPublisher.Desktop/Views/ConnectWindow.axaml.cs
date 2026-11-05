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

        // Closing the window stops a running sign-in: otherwise its loopback port stays taken for up to 10 minutes and a later try says
        // "port in use", and a late browser answer would still save an account in the background.
        Closed += (_, _) =>
        {
            if (DataContext is ConnectViewModel model && model.CancelCommand.CanExecute(null))
            {
                model.CancelCommand.Execute(null);
            }
        };
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
