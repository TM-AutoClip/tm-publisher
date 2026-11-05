using System.Net;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Platforms;

namespace TMPublisher.Tests.Desktop;

/// <summary>End to end with a fake platform: guide, required inputs, connect + test, stored account and secrets, test again, group.</summary>
public sealed class AccountFlowTests
{
    [AvaloniaFact]
    public async Task FakePlatformConnectsTestsAndJoinsAGroup()
    {
        var http = new FakeHttp().On(HttpMethod.Get, "fake.example/me", (request, _) =>
            request.Headers.Authorization?.Parameter == "good-token"
                ? FakeHttp.Response(HttpStatusCode.OK, "{\"id\":\"42\",\"name\":\"tester\"}")
                : FakeHttp.Response(HttpStatusCode.Unauthorized, "{\"error\":\"bad token\"}"));
        using var h = new DesktopHarness(s => s.AddSingleton<IPlatform>(new FakePlatform(http)));
        var vault = h.Services.GetRequiredService<Vault>();
        h.Services.GetRequiredService<UiMasterPassword>().Provide("a long master password");
        await vault.CreateAsync(vault.PasswordProtector, CancellationToken.None);

        var connect = h.Services.GetRequiredService<ConnectViewModel>();
        var window = new ConnectWindow { DataContext = connect };
        window.Show();
        connect.Platform = connect.Platforms.Single(p => p.Platform.Code == "fake");
        connect.Steps.Single().HasLink.ShouldBeTrue();
        connect.Warnings.Single().ShouldBe("warning.fake");
        connect.CanConnect().ShouldBeFalse();

        connect.Fields.Single().Value = "bad-token";
        await connect.ConnectAsync();
        connect.Failed.ShouldBeTrue();
        (await h.Services.GetRequiredService<IAccountStore>().ListAsync(CancellationToken.None)).ShouldBeEmpty();

        connect.Fields.Single().Value = "good-token";
        await connect.ConnectAsync();
        connect.Succeeded.ShouldBeTrue(connect.Status);
        window.Close();

        var account = (await h.Services.GetRequiredService<IAccountStore>().ListAsync(CancellationToken.None)).Single();
        account.ExternalId.ShouldBe("42");
        account.Handle.ShouldBe("@tester");
        var secrets = await h.Services.GetRequiredService<ICredentialStore>().GetAsync(account.Id, CancellationToken.None);
        secrets[CredentialKeys.AccessToken].ShouldBe("good-token");

        var accounts = h.Services.GetServices<PageViewModelBase>().OfType<AccountsViewModel>().Single();
        await accounts.ActivateAsync();
        var row = accounts.Rows.Single();
        await accounts.TestCommand.ExecuteAsync(row);
        row.TestResult.ShouldContain("tester");

        var groups = h.Services.GetServices<PageViewModelBase>().OfType<GroupsViewModel>().Single();
        await groups.ActivateAsync();
        groups.Name = "Main";
        await groups.SaveAsync();
        groups.Status.ShouldNotBeEmpty();
        (await h.Services.GetRequiredService<IGroupStore>().ListAsync(CancellationToken.None)).ShouldBeEmpty();
        groups.Members.Single().IsMember = true;
        await groups.SaveAsync();
        (await h.Services.GetRequiredService<IGroupStore>().ListAsync(CancellationToken.None)).Single().AccountIds.ShouldBe([account.Id]);
    }
}
