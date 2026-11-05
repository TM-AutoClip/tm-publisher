using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Accounts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>An account in the list.</summary>
public sealed partial class AccountRowViewModel(Account account, string platformName, string color, string stateText, bool needsReconnect) : ObservableObject
{
    /// <summary>Account.</summary>
    public Account Account { get; } = account;

    /// <summary>Platform name.</summary>
    public string PlatformName { get; } = platformName;

    /// <summary>Brand color.</summary>
    public string Color { get; } = color;

    /// <summary>Name.</summary>
    public string Name => Account.DisplayName;

    /// <summary>Handle.</summary>
    public string Handle => Account.Handle ?? string.Empty;

    /// <summary>State in words (reconnect warnings included).</summary>
    public string StateText { get; } = stateText;

    /// <summary>Whether the state needs attention.</summary>
    public bool NeedsReconnect { get; } = needsReconnect;

    /// <summary>Result of the last test.</summary>
    [ObservableProperty]
    public partial string TestResult { get; set; } = string.Empty;

    /// <summary>Remove was clicked once (a second click removes).</summary>
    [ObservableProperty]
    public partial bool ConfirmRemove { get; set; }
}
