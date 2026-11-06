using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using TMPublisher.Background.Composition;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Legacy;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Accounts;
using TMPublisher.Core.Notifications;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Posts;
using TMPublisher.Runtime.Paths;
using TMPublisher.Scheduling;
using TMPublisher.Storage.Crypto;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Scheduling;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Background;

/// <summary>
/// Runs as the system scheduler would start them, on a temporary data folder: every run is a new composition root (a new process), with a
/// fake clock, scripted platforms, recorded notifications and a recorded system scheduler. No network, no real scheduler, no keyring.
/// </summary>
internal sealed class RunHarness : IDisposable
{
    private readonly string _folder;
    private readonly bool _ownFolder;
    private readonly ScriptedPlatform[] _platforms;

    public RunHarness(string? folder = null, params ScriptedPlatform[] platforms)
    {
        _ownFolder = folder is null;
        _folder = folder ?? Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "run-" + Guid.NewGuid().ToString("N"));
        _platforms = platforms;
        Paths = new AppPaths(_folder);
        Data = Build();
    }

    public AppPaths Paths { get; }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero));

    public RecordingNotifier Notifier { get; } = new();

    public FakeSystemTrigger Trigger { get; } = new();

    public FixedPasswords Passwords { get; } = new("a long master password");

    /// <summary>The services the test uses to prepare the data (like the window).</summary>
    public ServiceProvider Data { get; private set; }

    public async Task CreateAsync(bool masterPassword = false)
    {
        var vault = Data.GetRequiredService<Vault>();
        await vault.CreateAsync(masterPassword ? vault.PasswordProtector : vault.AutomaticProtector!, CancellationToken.None);
    }

    public async Task<Account> AccountAsync(string platform, string name)
    {
        var account = new Account { Platform = platform, DisplayName = name, ExternalId = name, State = AccountState.Active };
        await Data.GetRequiredService<IAccountStore>().SaveAsync(account, CancellationToken.None);
        await Data.GetRequiredService<ICredentialStore>().SaveAsync(account.Id, Credentials.Empty.With((CredentialKeys.AccessToken, "t-" + name)), CancellationToken.None);
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
        await Data.GetRequiredService<IPostStore>().SaveAsync(post, CancellationToken.None);
        return post;
    }

    public async Task<Post> ReloadAsync(Post post) => (await Data.GetRequiredService<IPostStore>().GetAsync(post.Id, CancellationToken.None))!;

    /// <summary>One run in its own composition root (as a separate process would be).</summary>
    public async Task<RunOutcome> RunAsync()
    {
        using var run = Build();
        run.GetRequiredService<PostScheduler>().NetworkAvailable = () => true;
        return await run.GetRequiredService<DueRun>().RunAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        Data.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (_ownFolder)
        {
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
                // Windows may still hold a file for a moment; the temp folder is cleaned later.
            }
        }
    }

    private ServiceProvider Build()
    {
        var services = BackgroundServices.Build(Paths, s =>
        {
            s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IKeyProtector>();
            s.AddSingleton<IKeyProtector, TestKeyProtector>();
            s.AddSingleton<IKeyProtector>(new PasswordKeyProtector(Passwords, KdfParameters.Fast));
            s.RemoveAll<ISystemTrigger>();
            s.AddSingleton<ISystemTrigger>(Trigger);
            s.RemoveAll<ILegacyAutostart>();
            s.RemoveAll<INotifier>();
            s.AddSingleton<INotifier>(Notifier);
            s.RemoveAll<IPlatform>();
            foreach (var platform in _platforms)
            {
                s.AddSingleton<IPlatform>(platform);
            }

            s.AddSingleton(new TMPublisher.Tests.Platforms.FakeHttp().Transport);
        });
        services.GetRequiredService<TriggerArmer>().ProgramFile = () => Path.Combine(Path.GetTempPath(), "TMPublisher.exe");
        return services;
    }
}
