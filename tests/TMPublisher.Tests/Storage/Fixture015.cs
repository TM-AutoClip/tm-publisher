using Microsoft.Extensions.Time.Testing;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Quotas;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Stores;

namespace TMPublisher.Tests.Storage;

/// <summary>
/// <c>Fixtures/publisher-0.1.5.sqlite</c>: a database written by TM Publisher 0.1.5 (its hand-written SQL stores, the format of 0.1.3–0.1.5)
/// with the data below, encrypted with <see cref="Key"/>. <see cref="WriteAsync"/> writes the same data through the current stores in the same
/// order and at the same clock time, so the two files can be compared row by row.
/// </summary>
internal static class Fixture015
{
    /// <summary>The database key the fixture is encrypted with (bytes 1–32).</summary>
    public static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    /// <summary>The clock time the post store used (created / updated).</summary>
    public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 1, 9, 15, 30, TimeSpan.Zero).AddTicks(1234567);

    public static readonly Guid YouTube = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    public static readonly Guid Bluesky = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    public static readonly Guid AllGroup = Guid.Parse("a3bb189e-8bf9-3888-9912-ace4e6543002");

    public static readonly Guid EmptyGroup = Guid.Parse("b3bb189e-8bf9-3888-9912-ace4e6543003");

    public static readonly Guid ScheduledPost = Guid.Parse("e1f2a3b4-c5d6-4e7f-8091-a2b3c4d5e6f7");

    public static readonly Guid DraftPost = Guid.Parse("f1f2a3b4-c5d6-4e7f-8091-a2b3c4d5e6f7");

    public static readonly Guid DonePost = Guid.Parse("01f2a3b4-c5d6-4e7f-8091-a2b3c4d5e6f7");

    /// <summary>The fixture file next to the test program.</summary>
    public static string File => Path.Combine(AppContext.BaseDirectory, "Storage", "Fixtures", "publisher-0.1.5.sqlite");

    public static Account[] Accounts() =>
    [
        new()
        {
            Id = YouTube, Platform = "youtube", DisplayName = "Kanal Ğüşİöç", Handle = "@kanal", ExternalId = "UC123", State = AccountState.Active,
            ConnectedAtUtc = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.FromHours(3)).AddTicks(7),
        },
        new()
        {
            Id = Bluesky, Platform = "bluesky", DisplayName = "Sky", ExternalId = "did:plc:abc", State = AccountState.NeedsReconnect,
            ReconnectByUtc = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero), ConnectedAtUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        },
    ];

    public static (Guid Account, Credentials Values)[] Secrets() =>
    [
        (YouTube, Credentials.Empty.With(("access_token", "tok-1"), ("refresh_token", "ref \"q\" 1"))),
        (Bluesky, Credentials.Empty.With(("app_password", "p-2"))),
    ];

    public static Group[] Groups() =>
    [
        new() { Id = AllGroup, Name = "Hepsi", AccountIds = [Bluesky, YouTube] },
        new() { Id = EmptyGroup, Name = "Empty", AccountIds = [] },
    ];

    public static Post[] Posts() =>
    [
        new()
        {
            Id = ScheduledPost, Kind = PostKind.Video, Title = "Başlık", Body = "Gövde\nikinci satır #etiket", Tags = ["#a", "b"],
            Media = [new MediaItem(@"C:\videos\clip 1.mp4", MediaType.Video, 123456789L)],
            ScheduledAtUtc = new DateTimeOffset(2026, 11, 10, 20, 0, 0, TimeSpan.FromHours(3)), Status = PostStatus.Scheduled, GroupId = AllGroup,
            Targets =
            [
                new()
                {
                    Id = Guid.Parse("11111111-2222-4333-8444-555555555555"), AccountId = YouTube, Order = 0, Text = "yt text", Title = "yt title",
                    Keywords = ["k1", "k 2"], Options = new() { ["privacy"] = "unlisted", ["made_for_kids"] = "false" }, Status = TargetStatus.Pending,
                },
                new()
                {
                    Id = Guid.Parse("21111111-2222-4333-8444-555555555555"), AccountId = Bluesky, Order = 1, Text = "sky text", Title = string.Empty,
                    Status = TargetStatus.Published, ExternalId = "at://x/1", Url = "https://bsky.app/x/1", Message = "ok",
                    PublishedAtUtc = new DateTimeOffset(2026, 11, 2, 1, 2, 3, TimeSpan.Zero).AddTicks(99),
                },
            ],
        },
        new() { Id = DraftPost, Kind = PostKind.Text, Title = string.Empty, Body = "draft", Status = PostStatus.Draft },
        new()
        {
            Id = DonePost, Kind = PostKind.Album, Title = "al", Body = "b", Status = PostStatus.Done, ScheduledAtUtc = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            Media = [new MediaItem("/home/u/a.jpg", MediaType.Photo, 1), new MediaItem("/home/u/b.jpg", MediaType.Photo, 2)],
            Targets =
            [
                new()
                {
                    Id = Guid.Parse("31111111-2222-4333-8444-555555555555"), AccountId = YouTube, Order = 0, Text = "t", Title = string.Empty,
                    Status = TargetStatus.Failed, Message = "quota",
                },
            ],
        },
    ];

    public static AppSettings Settings() => new()
    {
        Language = "tr", NotificationsEnabled = false, NotificationSeconds = 15, StartWithSystem = false, WakeToRun = true, TikTokDraftByDefault = false,
    };

    public static TagSet[] TagSets() => [new() { Platform = "youtube", Tags = ["#yt", "shorts"] }, new() { Platform = "x", Tags = [] }];

    public static PublishLogEntry[] Log() =>
    [
        new(YouTube, "youtube", DonePost, new DateTimeOffset(2026, 10, 1, 8, 0, 5, TimeSpan.Zero), false),
        new(Bluesky, "bluesky", ScheduledPost, new DateTimeOffset(2026, 11, 2, 1, 2, 3, TimeSpan.Zero), true),
    ];

    /// <summary>Writes the fixture's data through the current stores, in the order 0.1.5 wrote it.</summary>
    public static async Task WriteAsync(PublisherDbContextFactory factory, CancellationToken cancellationToken)
    {
        foreach (var account in Accounts())
        {
            await new AccountStore(factory).SaveAsync(account, cancellationToken);
        }

        foreach (var (account, values) in Secrets())
        {
            await new CredentialStore(factory).SaveAsync(account, values, cancellationToken);
        }

        foreach (var group in Groups())
        {
            await new GroupStore(factory).SaveAsync(group, cancellationToken);
        }

        var posts = new PostStore(factory, new FakeTimeProvider(Now));
        foreach (var post in Posts())
        {
            await posts.SaveAsync(post, cancellationToken);
        }

        await new SettingsStore(factory).SaveAsync(Settings(), cancellationToken);
        foreach (var set in TagSets())
        {
            await new TagSetStore(factory).SaveAsync(set, cancellationToken);
        }

        foreach (var entry in Log())
        {
            await new PublishLogStore(factory).AddAsync(entry, cancellationToken);
        }
    }
}
