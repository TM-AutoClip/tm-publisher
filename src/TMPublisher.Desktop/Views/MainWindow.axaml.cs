using Avalonia.Controls;
using TMPublisher.Desktop.ViewModels;

namespace TMPublisher.Desktop.Views;

/// <summary>The main window (sidebar + page).</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ShellViewModel shell)
            {
                shell.ShowReportProblem = model => new ReportProblemWindow { DataContext = model }.ShowDialog(this);
            }
        };
    }
}
