using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Publishing;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Platforms.Telegram;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Platforms;

namespace TMPublisher.Tests.Desktop;

/// <summary>The compose screen: kind filter, targets, previews and "what is published = what the form shows".</summary>
public sealed class ComposeTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [AvaloniaFact]
    public async Task WhatIsStoredAndSentIsWhatTheFormShows()
    {
        using var h = await OpenAsync();
        var store = h.Services.GetRequiredService<IAccountStore>();
        var telegram = await AddAsync(store, PlatformCodes.Telegram, "Channel");
        var x = await AddAsync(store, PlatformCodes.X, "Me");
        var youtube = await AddAsync(store, PlatformCodes.YouTube, "Videos");
        var compose = Page<ComposeViewModel>(h);
        var view = new ComposeView { DataContext = compose };
        await compose.ActivateAsync();

        compose.Body = "Hello there";
        compose.Tags = "#news today";
        compose.Accounts.Single(a => a.Account.Id == youtube.Id).IsSupported.ShouldBeFalse();
        compose.Accounts.Single(a => a.Account.Id == youtube.Id).Reason.ShouldNotBeEmpty();
        compose.Accounts.Single(a => a.Account.Id == youtube.Id).IsChecked = true;
        compose.Accounts.Single(a => a.Account.Id == youtube.Id).IsChecked.ShouldBeFalse();
        compose.CanSend.ShouldBeFalse();

        compose.Accounts.Single(a => a.Account.Id == telegram.Id).IsChecked = true;
        compose.Accounts.Single(a => a.Account.Id == x.Id).IsChecked = true;
        compose.Targets.Count.ShouldBe(2);
        compose.Targets[1].TagLine = "#extra";
        compose.CostText.ShouldContain("0.015");
        var shown = compose.Targets.ToDictionary(t => t.Account.Id, t => t.Preview);
        shown[x.Id].ShouldBe("Hello there\n\n#extra #news #today");
        shown[telegram.Id].ShouldBe("Hello there\n\n#news #today");

        await compose.SendNowAsync();
        compose.Targets.ShouldBeEmpty();
        var post = (await h.Services.GetRequiredService<IPostStore>().ListByStatusAsync([PostStatus.Scheduled], None)).Single();
        post.Targets.Select(t => t.AccountId).ShouldBe([telegram.Id, x.Id]);
        foreach (var target in post.Targets)
        {
            target.Text.ShouldBe(shown[target.AccountId]);
        }

        var http = new FakeHttp().Json(HttpMethod.Post, "sendMessage", "{\"ok\":true,\"result\":{\"message_id\":5,\"chat\":{\"username\":\"c\"}}}");
        var publisher = new TelegramPublisher(http.Transport, NullLogger<TelegramPublisher>.Instance);
        var creds = Credentials.Empty.With((CredentialKeys.BotToken, "1:x"), (CredentialKeys.ChatId, "@c"));
        await publisher.PublishAsync(new PublishRequest(telegram, creds, post, post.Targets[0], null), null, None);
        http.Requests.Single().Body.ShouldContain("Hello there\\n\\n#news #today");
        view.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task GroupFillsTargetsInOrderAndHandChangesLeaveTheGroup()
    {
        using var h = await OpenAsync();
        var store = h.Services.GetRequiredService<IAccountStore>();
        var a = await AddAsync(store, PlatformCodes.Bluesky, "A");
        var b = await AddAsync(store, PlatformCodes.Mastodon, "B");
        await h.Services.GetRequiredService<IGroupStore>().SaveAsync(new Group { Name = "Both", AccountIds = [b.Id, a.Id] }, None);
        var compose = Page<ComposeViewModel>(h);
        await compose.ActivateAsync();
        compose.Body = "x";

        compose.SelectedGroup = compose.Groups.Single(g => g.Name == "Both");
        compose.Targets.Select(t => t.Account.Id).ShouldBe([b.Id, a.Id]);
        compose.Targets[0].ShowPrivacy.ShouldBeTrue();

        compose.Accounts.Single(p => p.Account.Id == b.Id).IsChecked = false;
        compose.SelectedGroup!.Group.ShouldBeNull();
        compose.Targets.Select(t => t.Account.Id).ShouldBe([a.Id]);
    }

    [AvaloniaFact]
    public async Task NoTargetsMeansNothingIsSent()
    {
        using var h = await OpenAsync();
        var compose = Page<ComposeViewModel>(h);
        await compose.ActivateAsync();
        compose.Body = "x";
        await compose.SendNowAsync();
        compose.Status.ShouldNotBeEmpty();
        (await h.Services.GetRequiredService<IPostStore>().ListByStatusAsync([PostStatus.Scheduled], None)).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TooLongTextBlocksXAndShowsTheReason()
    {
        using var h = await OpenAsync();
        var x = await AddAsync(h.Services.GetRequiredService<IAccountStore>(), PlatformCodes.X, "Me");
        var compose = Page<ComposeViewModel>(h);
        await compose.ActivateAsync();
        compose.Accounts.Single(p => p.Account.Id == x.Id).IsChecked = true;
        compose.Body = new string('a', 300);
        compose.Targets.Single().IsBlocked.ShouldBeTrue();
        compose.Targets.Single().ProblemsText.ShouldContain("300");
        compose.CanSend.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task VideoKeepsTikTokAsDraftByDefaultAndStoresTheMode()
    {
        using var h = await OpenAsync();
        var tiktok = await AddAsync(h.Services.GetRequiredService<IAccountStore>(), PlatformCodes.TikTok, "Clips");
        var compose = Page<ComposeViewModel>(h);
        compose.FileExists = _ => true;
        await compose.ActivateAsync();
        var file = Path.Combine(h.Paths.DataDirectory, "clip.mp4");
        await File.WriteAllBytesAsync(file, new byte[100], None);
        compose.AddFile(file);
        compose.Kind.ShouldBe(PostKind.Video);
        compose.Accounts.Single().IsChecked = true;
        compose.Targets.Single().TikTokDirect.ShouldBeFalse();
        compose.Targets.Single().Note.ShouldNotBeEmpty();
        await compose.SaveDraftAsync();
        var post = (await h.Services.GetRequiredService<IPostStore>().ListByStatusAsync([PostStatus.Draft], None)).Single();
        post.Targets.Single().Options[TargetOptionKeys.TikTokMode].ShouldBe("draft");
        post.Targets.Single().AccountId.ShouldBe(tiktok.Id);
    }

    [AvaloniaFact]
    public async Task EditingAPostWithChangedOptionsRestoresEveryTarget()
    {
        using var h = await OpenAsync();
        var store = h.Services.GetRequiredService<IAccountStore>();
        var first = await AddAsync(store, PlatformCodes.YouTube, "One");
        var second = await AddAsync(store, PlatformCodes.YouTube, "Two");
        var post = await StoreVideoPostAsync(h, PostStatus.Scheduled,
            Target(first, TargetStatus.Pending, new() { [TargetOptionKeys.Privacy] = "public", [TargetOptionKeys.MadeForKids] = "true" }),
            Target(second, TargetStatus.Pending, new() { [TargetOptionKeys.Privacy] = "unlisted", [TargetOptionKeys.Tags] = "#own" }));
        var compose = Page<ComposeViewModel>(h);
        compose.FileExists = _ => true;

        await compose.EditAsync(post);

        compose.IsEditing.ShouldBeTrue();
        compose.Targets.Select(t => t.Privacy!.Code).ShouldBe(["public", "unlisted"]);
        compose.Targets[0].MadeForKids.ShouldBeTrue();
        compose.Targets[1].TagLine.ShouldBe("#own");
        compose.Targets[1].Target.Options[TargetOptionKeys.Privacy].ShouldBe("unlisted");
    }

    [AvaloniaFact]
    public async Task TypingInATargetBoxKeepsItsCard()
    {
        using var h = await OpenAsync();
        var store = h.Services.GetRequiredService<IAccountStore>();
        var a = await AddAsync(store, PlatformCodes.Bluesky, "A");
        var b = await AddAsync(store, PlatformCodes.Mastodon, "B");
        var compose = Page<ComposeViewModel>(h);
        await compose.ActivateAsync();
        compose.Body = "x";
        compose.Accounts.Single(p => p.Account.Id == a.Id).IsChecked = true;
        compose.Accounts.Single(p => p.Account.Id == b.Id).IsChecked = true;
        var card = compose.Targets[1];
        var changes = 0;
        compose.Targets.CollectionChanged += (_, _) => changes++;

        card.TagLine = "#a";
        card.TagLine = "#ab";

        changes.ShouldBe(0);
        compose.Targets[1].ShouldBeSameAs(card);
        card.Preview.ShouldContain("#ab");

        compose.Accounts.Single(p => p.Account.Id == a.Id).IsChecked = false;
        compose.Targets.Single().ShouldBeSameAs(card);
    }

    [AvaloniaFact]
    public async Task EditingAPartlySentPostNeverSendsAPublishedTargetAgain()
    {
        using var h = await OpenAsync();
        var store = h.Services.GetRequiredService<IAccountStore>();
        var done = await AddAsync(store, PlatformCodes.YouTube, "Done");
        var open = await AddAsync(store, PlatformCodes.YouTube, "Open");
        var published = Target(done, TargetStatus.Published, []);
        published.ExternalId = "v1";
        published.Url = "https://example.com/v1";
        var post = await StoreVideoPostAsync(h, PostStatus.Scheduled, published, Target(open, TargetStatus.Pending, []));
        var compose = Page<ComposeViewModel>(h);
        compose.FileExists = _ => true;
        await compose.EditAsync(post);
        compose.Status.ShouldNotBeEmpty();

        compose.Body = "Changed text";
        await compose.SaveDraftAsync();

        var stored = (await h.Services.GetRequiredService<IPostStore>().GetAsync(post.Id, None))!;
        stored.Targets.Count.ShouldBe(2);
        var kept = stored.Targets.Single(t => t.AccountId == done.Id);
        kept.Status.ShouldBe(TargetStatus.Published);
        kept.ExternalId.ShouldBe("v1");
        kept.Url.ShouldBe("https://example.com/v1");
        stored.Targets.Single(t => t.AccountId == open.Id).Status.ShouldBe(TargetStatus.Pending);
    }

    [AvaloniaFact]
    public async Task AnEditIsRefusedWhenThePostWasSentMeanwhile()
    {
        using var h = await OpenAsync();
        var account = await AddAsync(h.Services.GetRequiredService<IAccountStore>(), PlatformCodes.YouTube, "One");
        var post = await StoreVideoPostAsync(h, PostStatus.Scheduled, Target(account, TargetStatus.Pending, []));
        var compose = Page<ComposeViewModel>(h);
        compose.FileExists = _ => true;
        await compose.EditAsync(post);

        // The scheduler sends it while the form is open.
        var posts = h.Services.GetRequiredService<IPostStore>();
        var sent = (await posts.GetAsync(post.Id, None))!;
        sent.Status = PostStatus.Done;
        sent.Targets[0].Status = TargetStatus.Published;
        await posts.SaveAsync(sent, None);

        await compose.SaveDraftAsync();

        compose.IsEditing.ShouldBeTrue();
        compose.Status.ShouldNotBeEmpty();
        var stored = (await posts.GetAsync(post.Id, None))!;
        stored.Status.ShouldBe(PostStatus.Done);
        stored.Targets.Single().Status.ShouldBe(TargetStatus.Published);
    }

    private static PostTarget Target(Account account, TargetStatus status, Dictionary<string, string> options) =>
        new() { AccountId = account.Id, Title = "Title", Text = "Body", Status = status, Options = options };

    private static async Task<Post> StoreVideoPostAsync(DesktopHarness h, PostStatus status, params PostTarget[] targets)
    {
        var file = Path.Combine(h.Paths.DataDirectory, "clip.mp4");
        await File.WriteAllBytesAsync(file, new byte[100], None);
        var post = new Post
        {
            Kind = PostKind.Video,
            Title = "Title",
            Body = "Body",
            Status = status,
            ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            Media = [new MediaItem(file, MediaType.Video, 100)],
            Targets = [.. targets],
        };
        for (var i = 0; i < targets.Length; i++)
        {
            targets[i].PostId = post.Id;
            targets[i].Order = i;
        }

        await h.Services.GetRequiredService<IPostStore>().SaveAsync(post, None);
        return (await h.Services.GetRequiredService<IPostStore>().GetAsync(post.Id, None))!;
    }

    private static T Page<T>(DesktopHarness h)
        where T : notnull => h.Services.GetRequiredService<T>();

    private static async Task<DesktopHarness> OpenAsync()
    {
        var h = new DesktopHarness();
        h.Services.GetRequiredService<TMPublisher.Desktop.Startup.UiMasterPassword>().Provide("a long master password");
        var vault = h.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.PasswordProtector, None);
        return h;
    }

    private static async Task<Account> AddAsync(IAccountStore store, string platform, string name)
    {
        var account = new Account { Platform = platform, DisplayName = name, ExternalId = name };
        await store.SaveAsync(account, None);
        return account;
    }
}
