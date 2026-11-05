using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// An account in the compose screen. Accounts whose platform cannot take this kind of post are grey with the reason and cannot be ticked.
/// </summary>
public sealed partial class AccountPickViewModel(Account account, IPlatform platform, Action<AccountPickViewModel> toggled) : ObservableObject
{
    private bool _quiet;

    /// <summary>Account.</summary>
    public Account Account { get; } = account;

    /// <summary>Platform.</summary>
    public IPlatform Platform { get; } = platform;

    /// <summary>"Platform · name".</summary>
    public string Title => Platform.DisplayName + " · " + Account.DisplayName;

    /// <summary>Platform color.</summary>
    public string Color => Platform.Color;

    /// <summary>Ticked.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>The platform can take this kind of post.</summary>
    [ObservableProperty]
    public partial bool IsSupported { get; set; } = true;

    /// <summary>Why it cannot (localized), empty when it can.</summary>
    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    /// <summary>Changes the tick without reporting it as the user's choice.</summary>
    public void Set(bool value)
    {
        _quiet = true;
        try
        {
            IsChecked = value;
        }
        finally
        {
            _quiet = false;
        }
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (value && !IsSupported)
        {
            _quiet = true;
            IsChecked = false;
            _quiet = false;
            return;
        }

        if (!_quiet)
        {
            toggled(this);
        }
    }
}
