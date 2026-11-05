using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Tests.Storage;

public sealed class StoreTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task PostsRoundTripWithOrderedTargets()
    {
        using var h = await StorageHarness.CreatedAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero));
        var store = new PostStore(h.Factory, clock);
        var post = new Post
        {
            Kind = PostKind.Video,
            Title = "T",
            Tags = ["a", "b"],
            Media = [new MediaItem("v.mp4", MediaType.Video, 10)],
            Status = PostStatus.Scheduled,
            ScheduledAtUtc = clock.GetUtcNow().AddHours(2),
        };
        post.Targets.Add(new PostTarget { AccountId = Guid.NewGuid(), Order = 1, Text = "second" });
        post.Targets.Add(new PostTarget { AccountId = Guid.NewGuid(), Order = 0, Text = "first", Options = { ["privacy"] = "private" } });
        await store.SaveAsync(post, None);

        var loaded = (await store.GetAsync(post.Id, None))!;
        loaded.Tags.ShouldBe(["a", "b"]);
        loaded.Media.Single().Path.ShouldBe("v.mp4");
        loaded.Targets.Select(t => t.Text).ShouldBe(["first", "second"]);
        loaded.Targets[0].Options["privacy"].ShouldBe("private");
        (await store.NextScheduledAsync(None))!.Id.ShouldBe(post.Id);

        loaded.Targets.RemoveAt(1);
        loaded.Title = "changed";
        await store.SaveAsync(loaded, None);
        var again = (await store.GetAsync(post.Id, None))!;
        again.Title.ShouldBe("changed");
        again.Targets.Count.ShouldBe(1);

        var target = again.Targets[0];
        target.Status = TargetStatus.Published;
        target.Url = "https://example.com/p/1";
        await store.SaveTargetAsync(target, None);
        (await store.GetAsync(post.Id, None))!.Targets[0].Status.ShouldBe(TargetStatus.Published);

        var range = await store.ListAsync(clock.GetUtcNow(), clock.GetUtcNow().AddDays(1), None);
        range.Single().Id.ShouldBe(post.Id);
    }

    [Fact]
    public async Task AccountDeleteRemovesSecretsAndMemberships()
    {
        using var h = await StorageHarness.CreatedAsync();
        var accounts = new AccountStore(h.Factory);
        var credentials = new CredentialStore(h.Factory);
        var groups = new GroupStore(h.Factory);
        var account = new Account { Platform = "bluesky", DisplayName = "A" };
        await accounts.SaveAsync(account, None);
        await credentials.SaveAsync(account.Id, Credentials.Empty.With(("access_token", "t")), None);
        await groups.SaveAsync(new Group { Name = "G", AccountIds = [account.Id] }, None);
        (await credentials.GetAsync(account.Id, None))["access_token"].ShouldBe("t");

        await accounts.DeleteAsync(account.Id, None);
        (await accounts.ListAsync(None)).ShouldBeEmpty();
        (await credentials.GetAsync(account.Id, None)).Values.ShouldBeEmpty();
        (await groups.ListAsync(None)).Single().AccountIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task SettingsAndTagSetsRoundTrip()
    {
        using var h = await StorageHarness.CreatedAsync();
        var settings = new SettingsStore(h.Factory);
        (await settings.GetAsync(None)).NotificationSeconds.ShouldBe(60);
        await settings.SaveAsync(new AppSettings { Language = "tr", NotificationSeconds = 15 }, None);
        (await settings.GetAsync(None)).Language.ShouldBe("tr");

        var tags = new TagSetStore(h.Factory);
        await tags.SaveAsync(new TagSet { Platform = "x", Tags = ["news"] }, None);
        await tags.SaveAsync(new TagSet { Platform = "x", Tags = ["news", "tech"] }, None);
        (await tags.ListAsync(None)).Single().Tags.ShouldBe(["news", "tech"]);
    }
}
