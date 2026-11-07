using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Tests.Storage;

/// <summary>
/// The file format did not change when the stores moved from hand-written SQL (0.1.3–0.1.5) to EF Core: a database written by 0.1.5 opens
/// with all its data, takes changes, and the current stores write exactly the rows 0.1.5 wrote.
/// </summary>
public sealed class FormatCompatibilityTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ADatabaseWrittenBy015OpensWithAllItsData()
    {
        using var paths = new TempPaths();
        var factory = await OpenFixtureAsync(paths);

        var accounts = await new AccountStore(factory).ListAsync(Token);
        accounts.Select(a => a.Id).ShouldBe([Fixture015.Bluesky, Fixture015.YouTube]);
        foreach (var expected in Fixture015.Accounts())
        {
            var account = accounts.Single(a => a.Id == expected.Id);
            account.Platform.ShouldBe(expected.Platform);
            account.DisplayName.ShouldBe(expected.DisplayName);
            account.Handle.ShouldBe(expected.Handle);
            account.ExternalId.ShouldBe(expected.ExternalId);
            account.State.ShouldBe(expected.State);
            account.ReconnectByUtc.ShouldBe(expected.ReconnectByUtc);
            account.ConnectedAtUtc.UtcTicks.ShouldBe(expected.ConnectedAtUtc.UtcTicks);
            account.ConnectedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        }

        foreach (var (account, values) in Fixture015.Secrets())
        {
            (await new CredentialStore(factory).GetAsync(account, Token)).Values.ShouldBe(values.Values, ignoreOrder: true);
        }

        var groups = await new GroupStore(factory).ListAsync(Token);
        groups.Select(g => g.Name).ShouldBe(["Empty", "Hepsi"]);
        groups.Single(g => g.Id == Fixture015.AllGroup).AccountIds.ShouldBe([Fixture015.Bluesky, Fixture015.YouTube]);
        groups.Single(g => g.Id == Fixture015.EmptyGroup).AccountIds.ShouldBeEmpty();

        var posts = new PostStore(factory, new FakeTimeProvider(Fixture015.Now));
        foreach (var expected in Fixture015.Posts())
        {
            PostShouldBe((await posts.GetAsync(expected.Id, Token))!, expected);
        }

        (await posts.NextScheduledAsync(Token))!.Id.ShouldBe(Fixture015.ScheduledPost);
        (await posts.ListByStatusAsync([PostStatus.Draft, PostStatus.Done], Token)).Select(p => p.Id).ShouldBe([Fixture015.DonePost, Fixture015.DraftPost]);
        var november = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        (await posts.ListAsync(november, november.AddMonths(1), Token)).Select(p => p.Id).ShouldBe([Fixture015.DraftPost, Fixture015.ScheduledPost]);

        var settings = await new SettingsStore(factory).GetAsync(Token);
        settings.Language.ShouldBe("tr");
        settings.NotificationsEnabled.ShouldBeFalse();
        settings.NotificationSeconds.ShouldBe(15);
        settings.StartWithSystem.ShouldBeFalse();
        settings.WakeToRun.ShouldBeTrue();
        settings.TikTokDraftByDefault.ShouldBeFalse();

        var sets = await new TagSetStore(factory).ListAsync(Token);
        sets.Single(s => s.Platform == "youtube").Tags.ShouldBe(["#yt", "shorts"]);
        sets.Single(s => s.Platform == "x").Tags.ShouldBeEmpty();

        var log = await new PublishLogStore(factory).ListAsync(null, DateTimeOffset.MinValue.AddYears(1), Token);
        log.Select(l => (l.AccountId, l.Platform, l.PostId, l.AtUtc.UtcTicks, l.Success))
            .ShouldBe(Fixture015.Log().Reverse().Select(l => (l.AccountId, l.Platform, l.PostId, l.AtUtc.UtcTicks, l.Success)));
        (await new PublishLogStore(factory).ListAsync(Fixture015.YouTube, DateTimeOffset.MinValue.AddYears(1), Token)).Single().Success.ShouldBeFalse();
    }

    [Fact]
    public async Task TheStoresWriteExactlyTheRowsAndSchemaOf015()
    {
        var passphrase = Convert.ToHexString(Fixture015.Key);
        var expectedRows = await RawTables.RowsAsync(Fixture015.File, passphrase, Token);
        var expectedSchema = await RawTables.SchemaAsync(Fixture015.File, passphrase, Token);
        expectedRows.Count.ShouldBe(17);

        // A new file with the fixture's key (the vault would make a random one), so the two files can be read the same way.
        using var paths = new TempPaths();
        var key = new DatabaseKey();
        key.Load(Fixture015.Key);
        var factory = new PublisherDbContextFactory(paths, key);
        await using (var context = factory.CreateDbContext())
        {
            (await Schema.EnsureAsync(context, Token)).ShouldBeTrue();
        }

        await Fixture015.WriteAsync(factory, Token);
        (await RawTables.RowsAsync(paths.DatabaseFile, passphrase, Token)).ShouldBe(expectedRows);
        (await RawTables.SchemaAsync(paths.DatabaseFile, passphrase, Token)).ShouldBe(expectedSchema);
    }

    [Fact]
    public async Task ADatabaseWrittenBy015TakesChanges()
    {
        using var paths = new TempPaths();
        var factory = await OpenFixtureAsync(paths);
        var clock = new FakeTimeProvider(Fixture015.Now.AddDays(1));
        var posts = new PostStore(factory, clock);

        // An edit keeps the target that already reached its platform; the claim compares the stored time (ticks) of 0.1.5.
        var post = (await posts.GetAsync(Fixture015.ScheduledPost, Token))!;
        post.Title = "edited";
        post.Targets.RemoveAll(t => t.Status == TargetStatus.Published);
        (await posts.SaveEditAsync(post, [PostStatus.Scheduled], Token)).ShouldBeTrue();
        var edited = (await posts.GetAsync(Fixture015.ScheduledPost, Token))!;
        edited.Title.ShouldBe("edited");
        edited.Targets.Count.ShouldBe(2);
        edited.CreatedAtUtc.ShouldBe(Fixture015.Now);
        edited.UpdatedAtUtc.ShouldBe(clock.GetUtcNow());
        (await posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, post.ScheduledAtUtc!.Value.AddMinutes(1), PostStatus.Publishing), Token))
            .ShouldBeFalse();
        (await posts.TryMoveAsync(new PostClaim(post.Id, PostStatus.Scheduled, post.ScheduledAtUtc, PostStatus.Publishing), Token)).ShouldBeTrue();
        (await posts.GetAsync(post.Id, Token))!.Status.ShouldBe(PostStatus.Publishing);
        (await posts.TryMoveAsync(new PostClaim(Fixture015.DraftPost, PostStatus.Draft, null, PostStatus.Canceled), Token)).ShouldBeTrue();

        // Deleting an account removes its secrets and its place in the groups; deleting a post removes its targets.
        await new AccountStore(factory).DeleteAsync(Fixture015.Bluesky, Token);
        (await new CredentialStore(factory).GetAsync(Fixture015.Bluesky, Token)).Values.ShouldBeEmpty();
        (await new GroupStore(factory).ListAsync(Token)).Single(g => g.Id == Fixture015.AllGroup).AccountIds.ShouldBe([Fixture015.YouTube]);
        await posts.DeleteAsync(Fixture015.DonePost, Token);
        (await posts.GetAsync(Fixture015.DonePost, Token)).ShouldBeNull();
        var passphrase = Convert.ToHexString(Fixture015.Key);
        (await RawTables.RowsAsync(paths.DatabaseFile, passphrase, Token)).ShouldNotContain(r => r.Contains("31111111-2222-4333-8444-555555555555", StringComparison.Ordinal));
    }

    /// <summary>Copies the fixture into a data folder and opens it the way a run does with a handed-over key.</summary>
    private static async Task<PublisherDbContextFactory> OpenFixtureAsync(TempPaths paths)
    {
        File.Copy(Fixture015.File, paths.DatabaseFile);
        var key = new DatabaseKey();
        var vault = new Vault(paths, key, [new PasswordKeyProtector(new FixedPasswords("unused password"), KdfParameters.Fast)], NullLogger<Vault>.Instance);
        await vault.OpenWithKeyAsync(Fixture015.Key, Token);
        return new PublisherDbContextFactory(paths, key);
    }

    private static void PostShouldBe(Post post, Post expected)
    {
        post.Kind.ShouldBe(expected.Kind);
        post.Title.ShouldBe(expected.Title);
        post.Body.ShouldBe(expected.Body);
        post.Tags.ShouldBe(expected.Tags);
        post.Media.ShouldBe(expected.Media);
        post.ScheduledAtUtc.ShouldBe(expected.ScheduledAtUtc);
        post.Status.ShouldBe(expected.Status);
        post.GroupId.ShouldBe(expected.GroupId);
        post.CreatedAtUtc.ShouldBe(Fixture015.Now);
        post.UpdatedAtUtc.ShouldBe(Fixture015.Now);
        post.Targets.Count.ShouldBe(expected.Targets.Count);
        foreach (var (target, wanted) in post.Targets.Zip(expected.Targets))
        {
            target.Id.ShouldBe(wanted.Id);
            target.PostId.ShouldBe(expected.Id);
            target.AccountId.ShouldBe(wanted.AccountId);
            target.Order.ShouldBe(wanted.Order);
            target.Text.ShouldBe(wanted.Text);
            target.Title.ShouldBe(wanted.Title);
            target.Keywords.ShouldBe(wanted.Keywords);
            target.Options.ShouldBe(wanted.Options);
            target.Status.ShouldBe(wanted.Status);
            target.ExternalId.ShouldBe(wanted.ExternalId);
            target.Url.ShouldBe(wanted.Url);
            target.Message.ShouldBe(wanted.Message);
            target.PublishedAtUtc.ShouldBe(wanted.PublishedAtUtc);
        }
    }
}
