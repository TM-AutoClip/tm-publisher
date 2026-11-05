using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Desktop.Accounts;
using TMPublisher.Desktop.Localization;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>Connected accounts: add (connection window), test, connect again, remove.</summary>
public sealed partial class AccountsViewModel(IAccountStore accounts, IPlatformDirectory platforms, AccountService service, Func<ConnectViewModel> connect,
    TimeProvider clock, Localizer text, ILogger<AccountsViewModel> logger) : PageViewModelBase, IPageInfo
{
    /// <inheritdoc />
    public static string TitleKey => "nav.accounts";

    /// <inheritdoc />
    public static string Glyph => "👤";

    /// <inheritdoc />
    public static int Order => 40;

    /// <summary>Accounts.</summary>
    public ObservableCollection<AccountRowViewModel> Rows { get; } = [];

    /// <summary>Opens the connection window (set by the view).</summary>
    public Func<ConnectViewModel, Task>? ShowConnect { get; set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        Rows.Clear();
        var now = clock.GetUtcNow();
        foreach (var account in await accounts.ListAsync(CancellationToken.None).ConfigureAwait(true))
        {
            var platform = platforms.Find(account.Platform);
            var reconnect = account.State == AccountState.NeedsReconnect;
            var state = reconnect ? text["account.state.reconnect"]
                : account.ReconnectByUtc is { } by && by - now < TimeSpan.FromDays(7) ? text.Format("account.state.reconnect_by", by.ToLocalTime().ToString("d", text.Culture))
                : text["account.state.active"];
            Rows.Add(new AccountRowViewModel(account, platform?.DisplayName ?? account.Platform, platform?.Color ?? "#888888", state,
                reconnect || account.ReconnectByUtc < now.AddDays(7)));
        }

        Status = Rows.Count == 0 ? text["accounts.empty"] : string.Empty;
    }

    /// <summary>Adds an account.</summary>
    [RelayCommand]
    private Task AddAsync() => OpenConnectAsync(null);

    /// <summary>Connects an existing account again (same platform pre-selected).</summary>
    [RelayCommand]
    private Task ReconnectAsync(AccountRowViewModel? row) => OpenConnectAsync(row?.Account.Platform);

    /// <summary>Tests a stored account.</summary>
    [RelayCommand]
    private async Task TestAsync(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.TestResult = text["account.testing"];
        try
        {
            var identity = await service.TestAsync(row.Account, CancellationToken.None).ConfigureAwait(true);
            row.TestResult = text.Format("account.test_ok", identity.DisplayName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account test failed");
            row.TestResult = text.Format("account.test_failed", ex.Message);
        }
    }

    /// <summary>Removes after a second click.</summary>
    [RelayCommand]
    private async Task RemoveAsync(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!row.ConfirmRemove)
        {
            row.ConfirmRemove = true;
            return;
        }

        await service.DeleteAsync(row.Account, CancellationToken.None).ConfigureAwait(true);
        await ActivateAsync().ConfigureAwait(true);
    }

    private async Task OpenConnectAsync(string? platform)
    {
        var model = connect();
        if (platform is not null)
        {
            model.Platform = model.Platforms.FirstOrDefault(p => p.Platform.Code == platform);
        }

        if (ShowConnect is { } show)
        {
            await show(model).ConfigureAwait(true);
        }

        await ActivateAsync().ConfigureAwait(true);
    }
}
