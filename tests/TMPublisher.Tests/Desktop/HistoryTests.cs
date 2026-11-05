using System.Net;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Platforms.Common;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Platforms;

namespace TMPublisher.Tests.Desktop;

public sealed class HistoryTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [AvaloniaFact]
    public async Task OpeningMakesNoRequestAndFetchReadsOnlyOnClick()
    {
        var http = new FakeHttp()
            .Json(HttpMethod.Get, "api/v1/statuses/111", "{\"id\":\"111\",\"favourites_count\":7,\"reblogs_count\":2,\"replies_count\":1}")
            .Json(HttpMethod.Get, "2/tweets", "{\"data\":[{\"id\":\"222\",\"public_metrics\":{\"impression_count\":900,\"like_count\":12,\"retweet_count\":3,\"reply_count\":4}}]}");
        using var h = new DesktopHarness(s => s.AddSingleton(http.Transport));
        h.Services.GetRequiredService<TMPublisher.Desktop.Startup.UiMasterPassword>().Provide("a long master password");
        var vault = h.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.PasswordProtector, None);

        var store = h.Services.GetRequiredService<IAccountStore>();
        var secrets = h.Services.GetRequiredService<ICredentialStore>();
        var mastodon = new Account { Platform = PlatformCodes.Mastodon, DisplayName = "M", ExternalId = "m" };
        var x = new Account { Platform = PlatformCodes.X, DisplayName = "X", ExternalId = "x" };
        await store.SaveAsync(mastodon, None);
        await store.SaveAsync(x, None);
        await secrets.SaveAsync(mastodon.Id, Credentials.Empty.With((CredentialKeys.Server, "https://social.example"), (CredentialKeys.AccessToken, "a")), None);
        await secrets.SaveAsync(x.Id, Credentials.Empty.With((CredentialKeys.AccessToken, "b"), (CredentialKeys.ClientId, "c")), None);
        var now = DateTimeOffset.UtcNow;
        var post = new Post { Kind = PostKind.Text, Body = "Sent", Status = PostStatus.Done, ScheduledAtUtc = now.AddHours(-2), CreatedAtUtc = now, UpdatedAtUtc = now };
        post.Targets =
        [
            new PostTarget { PostId = post.Id, AccountId = mastodon.Id, Order = 0, Status = TargetStatus.Published, ExternalId = "111", Url = "https://social.example/@m/111" },
            new PostTarget { PostId = post.Id, AccountId = x.Id, Order = 1, Status = TargetStatus.Published, ExternalId = "222", Url = "https://x.com/i/status/222" },
        ];
        await h.Services.GetRequiredService<IPostStore>().SaveAsync(post, None);
        await h.Services.GetRequiredService<IPublishLog>().AddAsync(new PublishLogEntry(x.Id, PlatformCodes.X, post.Id, now.AddHours(-2), true), None);

        var history = h.Services.GetRequiredService<HistoryViewModel>();
        var view = new HistoryView { DataContext = history };
        await history.ActivateAsync();
        http.Requests.ShouldBeEmpty();
        history.Rows.Single().Targets.Count.ShouldBe(2);
        history.CanFetch.ShouldBeTrue();
        history.FetchLabel.ShouldContain("0.005");
        history.Counts.ShouldContain(c => c.Contains("X", StringComparison.Ordinal));
        history.Quotas.ShouldNotBeEmpty();

        await history.FetchCommand.ExecuteAsync(null);
        http.Requests.Count.ShouldBe(2);
        history.Rows.Single().Targets[0].Stats.ShouldContain("7");
        history.Rows.Single().Targets[1].Stats.ShouldContain("900");
        view.ShouldNotBeNull();
    }
}
