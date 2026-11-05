using Avalonia.Controls;

namespace TMPublisher.Desktop.Views;

/// <summary>Unlock window (master password, or retry after a keyring error).</summary>
public sealed partial class UnlockWindow : Window
{
    /// <summary>Creates.</summary>
    public UnlockWindow() => InitializeComponent();
}
