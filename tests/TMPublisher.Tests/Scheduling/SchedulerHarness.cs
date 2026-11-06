using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Scheduling;
using TMPublisher.Scheduling.Accounts;
using TMPublisher.Storage.Stores;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Scheduling;

/// <summary>The scheduler on a real encrypted database, a fake clock and scripted platforms.</summary>
internal sealed class SchedulerHarness : IDisposable
{
    private readonly StorageHarness _storage;

    private SchedulerHarness(StorageHarness storage, params ScriptedPlatform[] platforms)
    {
        _storage = storage;
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero));
        Posts = new PostStore(storage.Factory, Clock);
        Accounts = new AccountStore(storage.Factory);
        Credentials = new CredentialStore(storage.Factory);
        Log = new PublishLogStore(storage.Factory);
        var directory = new PlatformDirectory(platforms);
        Gate = new FileRefreshGate(storage.Paths, TimeProvider.System, NullLogger<FileRefreshGate>.Instance);
        var renewer = new CredentialRenewer(directory, Credentials, Accounts, Gate, Clock, NullLogger<CredentialRenewer>.Instance);
        Renewer = renewer;
        var text = new KeyLocalizer();
        Sender = new PostSender(Posts, Accounts, Credentials, directory, renewer, new QuotaChecker(Log, Clock), Log, Notifier, text, Clock, NullLogger<PostSender>.Instance);
        Watcher = new ProcessingWatcher(Posts, Accounts, directory, renewer, Notifier, text, Clock, NullLogger<ProcessingWatcher>.Instance);
        Failing = new FailingPostStore(Posts);
        Scheduler = new PostScheduler(Failing, Sender, OldVersion, Watcher, Notifier, text, Clock, NullLogger<PostScheduler>.Instance);
        Scheduler.NetworkAvailable = () => true;
        Decisions = new PostDecisions(Posts, Clock, NullLogger<PostDecisions>.Instance);
    }

    public FakeTimeProvider Clock { get; }

    public PostStore Posts { get; }

    /// <summary>The store the scheduler uses: <see cref="Posts"/> with database errors on demand.</summary>
    public FailingPostStore Failing { get; }

    public AccountStore Accounts { get; }

    public CredentialStore Credentials { get; }

    public PublishLogStore Log { get; }

    public RecordingNotifier Notifier { get; } = new();

    public FakeOldVersion OldVersion { get; } = new();

    public CredentialRenewer Renewer { get; }

    public PostSender Sender { get; }

    public ProcessingWatcher Watcher { get; }

    public PostScheduler Scheduler { get; }

    public PostDecisions Decisions { get; }

    public FileRefreshGate Gate { get; }

    public TMPublisher.Core.Paths.IAppPaths Paths => _storage.Paths;

    public static async Task<SchedulerHarness> CreateAsync(params ScriptedPlatform[] platforms) =>
        new(await StorageHarness.CreatedAsync(), platforms);

    public async Task<Account> AccountAsync(string platform, string name, AccountState state = AccountState.Active)
    {
        var account = new Account { Platform = platform, DisplayName = name, ExternalId = name, State = state };
        await Accounts.SaveAsync(account, CancellationToken.None);
        await Credentials.SaveAsync(account.Id, TMPublisher.Core.Accounts.Credentials.Empty.With((CredentialKeys.AccessToken, "t-" + name)), CancellationToken.None);
        return account;
    }

    public async Task<Post> PostAsync(DateTimeOffset at, params Account[] targets)
    {
        var post = new Post
        {
            Kind = PostKind.Text,
            Body = "Hello",
            Status = PostStatus.Scheduled,
            ScheduledAtUtc = at,
            CreatedAtUtc = Clock.GetUtcNow(),
            UpdatedAtUtc = Clock.GetUtcNow(),
        };
        post.Targets = [.. targets.Select((a, i) => new PostTarget { PostId = post.Id, AccountId = a.Id, Order = i, Text = "Hello " + a.DisplayName })];
        await Posts.SaveAsync(post, CancellationToken.None);
        return post;
    }

    public async Task<Post> ReloadAsync(Post post) => (await Posts.GetAsync(post.Id, CancellationToken.None))!;

    /// <summary>Waits until the post reaches the state (timer callbacks continue on the thread pool).</summary>
    public async Task<Post> WaitForAsync(Post post, PostStatus status)
    {
        for (var i = 0; i < 200; i++)
        {
            var current = await ReloadAsync(post);
            if (current.Status == status)
            {
                return current;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        return await ReloadAsync(post);
    }

    public void Dispose()
    {
        Scheduler.Dispose();
        Watcher.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _storage.Dispose();
    }
}
