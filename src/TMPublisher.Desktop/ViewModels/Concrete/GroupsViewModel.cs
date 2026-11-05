using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Groups: a name and an ordered list of accounts. A post goes to a group or to accounts picked by hand; never to nobody.</summary>
public sealed partial class GroupsViewModel(IGroupStore groups, IAccountStore accounts, IPlatformDirectory platforms, Localizer text) : PageViewModelBase
{
    /// <inheritdoc />
    public override string TitleKey => "nav.groups";

    /// <inheritdoc />
    public override string Glyph => "👥";

    /// <inheritdoc />
    public override int Order => 50;

    /// <summary>Groups.</summary>
    public ObservableCollection<Group> Groups { get; } = [];

    /// <summary>Accounts of the edited group, members first in their order.</summary>
    public ObservableCollection<GroupMemberViewModel> Members { get; } = [];

    /// <summary>Edited group.</summary>
    [ObservableProperty]
    public partial Group? Selected { get; set; }

    /// <summary>Name of the edited group.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        Groups.Clear();
        foreach (var group in await groups.ListAsync(CancellationToken.None).ConfigureAwait(true))
        {
            Groups.Add(group);
        }

        Selected = Groups.FirstOrDefault();
        if (Selected is null)
        {
            await LoadMembersAsync(null).ConfigureAwait(true);
        }
    }

    partial void OnSelectedChanged(Group? value)
    {
        Name = value?.Name ?? string.Empty;
        _ = LoadMembersAsync(value);
    }

    /// <summary>Starts a new group.</summary>
    [RelayCommand]
    private Task NewAsync()
    {
        Selected = null;
        Name = string.Empty;
        return LoadMembersAsync(null);
    }

    /// <summary>Saves the edited group.</summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Name.Trim().Length == 0)
        {
            Status = text["groups.name_required"];
            return;
        }

        var members = Members.Where(m => m.IsMember).Select(m => m.Account.Id).ToList();
        if (members.Count == 0)
        {
            Status = text["groups.members_required"];
            return;
        }

        var group = Selected ?? new Group();
        group.Name = Name.Trim();
        group.AccountIds = members;
        await groups.SaveAsync(group, CancellationToken.None).ConfigureAwait(true);
        Status = text["groups.saved"];
        var id = group.Id;
        await ActivateAsync().ConfigureAwait(true);
        Selected = Groups.FirstOrDefault(g => g.Id == id);
    }

    /// <summary>Deletes the edited group.</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is { } group)
        {
            await groups.DeleteAsync(group.Id, CancellationToken.None).ConfigureAwait(true);
            await ActivateAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Moves an account one place up (sending order).</summary>
    [RelayCommand]
    private void MoveUp(GroupMemberViewModel? member)
    {
        if (member is not null && Members.IndexOf(member) is var i and > 0)
        {
            Members.Move(i, i - 1);
        }
    }

    /// <summary>Moves an account one place down.</summary>
    [RelayCommand]
    private void MoveDown(GroupMemberViewModel? member)
    {
        if (member is not null && Members.IndexOf(member) is var i && i >= 0 && i < Members.Count - 1)
        {
            Members.Move(i, i + 1);
        }
    }

    private async Task LoadMembersAsync(Group? group)
    {
        var all = await accounts.ListAsync(CancellationToken.None).ConfigureAwait(true);
        var order = group?.AccountIds ?? [];
        Members.Clear();
        foreach (var account in all.OrderBy(a => order.IndexOf(a.Id) is var i && i >= 0 ? i : int.MaxValue))
        {
            Members.Add(new GroupMemberViewModel(account, platforms.Find(account.Platform)?.DisplayName ?? account.Platform) { IsMember = order.Contains(account.Id) });
        }
    }
}
