using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Desktop;

/// <summary>The groups page shows and saves the members of the group that is selected, whatever order the loads finish in.</summary>
public sealed class GroupsTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [AvaloniaFact]
    public async Task QuickSelectionChangesShowTheLastGroupsMembers()
    {
        using var h = new DesktopHarness();
        h.Services.GetRequiredService<TMPublisher.Desktop.Startup.UiMasterPassword>().Provide("a long master password");
        var vault = h.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.PasswordProtector, None);
        var accounts = h.Services.GetRequiredService<IAccountStore>();
        var a = new Account { Platform = PlatformCodes.Bluesky, DisplayName = "A", ExternalId = "a" };
        var b = new Account { Platform = PlatformCodes.Mastodon, DisplayName = "B", ExternalId = "b" };
        await accounts.SaveAsync(a, None);
        await accounts.SaveAsync(b, None);
        var store = h.Services.GetRequiredService<IGroupStore>();
        await store.SaveAsync(new Group { Name = "First", AccountIds = [a.Id] }, None);
        await store.SaveAsync(new Group { Name = "Second", AccountIds = [b.Id] }, None);

        var page = h.Services.GetServices<PageViewModelBase>().OfType<GroupsViewModel>().Single();
        await page.ActivateAsync();
        var first = page.Groups.Single(g => g.Name == "First");
        var second = page.Groups.Single(g => g.Name == "Second");
        for (var i = 0; i < 5; i++)
        {
            page.Selected = first;
            page.Selected = second;
        }

        for (var i = 0; i < 100 && page.Members.Count < 2; i++)
        {
            await Task.Delay(10, None);
        }

        await Task.Delay(100, None);
        page.Name.ShouldBe("Second");
        page.Members.Where(m => m.IsMember).Select(m => m.Account.Id).ShouldBe([b.Id]);
    }
}
