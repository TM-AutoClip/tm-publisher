using Avalonia.Controls;
using TMPublisher.Desktop.ViewModels;

namespace TMPublisher.Desktop.Views;

/// <summary>Accounts page.</summary>
public sealed partial class AccountsView : UserControl
{
    /// <summary>Creates.</summary>
    public AccountsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AccountsViewModel model)
            {
                model.ShowConnect = async connect =>
                {
                    var window = new ConnectWindow { DataContext = connect };
                    if (TopLevel.GetTopLevel(this) is Window owner)
                    {
                        await window.ShowDialog(owner).ConfigureAwait(true);
                    }
                };
            }
        };
    }
}
