using CommunityToolkit.Mvvm.ComponentModel;
using TMPublisher.Core.Accounts;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>An account in the group editor (ticked = member).</summary>
public sealed partial class GroupMemberViewModel(Account account, string platformName) : ObservableObject
{
    /// <summary>Account.</summary>
    public Account Account { get; } = account;

    /// <summary>"Platform · name".</summary>
    public string Title => platformName + " · " + Account.DisplayName;

    /// <summary>Member.</summary>
    [ObservableProperty]
    public partial bool IsMember { get; set; }
}
