using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Background.Triggers;
using TMPublisher.Core.Posts;
using TMPublisher.Runtime.Paths;
using TMPublisher.Storage.Database;
using TMPublisher.Storage.Keys;
using TMPublisher.Storage.Stores;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Background;

/// <summary>
/// The published (trimmed, single-file) program, not the build next to the tests: a run (<c>--run-due</c>) on a copy of the 0.1.5 database
/// opens it through EF Core, reads the schedule, marks a late post "missed" (a claim), stops a post left "publishing" (a whole-post write) and
/// sets its next start. Nothing is sent: the only due post is late, the notifications of the fixture are off and no target is processing.
/// Runs only when <see cref="ProgramVariable"/> names the program (<c>tools/check-published.ps1</c> sets it); the Task Scheduler task it
/// creates has a test name and is removed again.
/// </summary>
public sealed class PublishedProgramTests
{
    /// <summary>The environment variable with the path of the published program.</summary>
    public const string ProgramVariable = "TMPUBLISHER_PUBLISHED_PROGRAM";

    private const string TestPrefix = "TMPublisherTest";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePublishedProgramRunsOnADatabaseWrittenBy015()
    {
        var program = Environment.GetEnvironmentVariable(ProgramVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(program), $"{ProgramVariable} does not name a published program (tools/check-published.ps1 sets it)");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the copied database's key is sealed with DPAPI, which exists only on Windows");
        File.Exists(program).ShouldBeTrue($"{ProgramVariable} names a file that does not exist: {program}");

        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "published-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(folder);
        var task = TestPrefix + " " + TriggerNames.For(paths).Split(' ')[^1];
        try
        {
            var factory = await PrepareAsync(paths);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var start = new ProcessStartInfo(program!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--run-due");
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(folder);
            start.Environment[TriggerNames.PrefixVariable] = TestPrefix;
            using var process = Process.Start(start)!;
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct))
            {
                limit.CancelAfter(TimeSpan.FromMinutes(2));
                await process.WaitForExitAsync(limit.Token);
            }

            var log = File.ReadAllText(Directory.GetFiles(paths.LogDirectory, "run-*.log").Single());
            process.ExitCode.ShouldBe(0, log);
            log.ShouldContain("Run ended (Done)");
            log.ShouldContain("UI library loaded: no");

            var posts = new PostStore(factory, TimeProvider.System);
            (await posts.GetAsync(Fixture015.ScheduledPost, Ct))!.Status.ShouldBe(PostStatus.Missed);
            (await posts.GetAsync(Fixture015.DraftPost, Ct))!.Status.ShouldBe(PostStatus.Stopped);
            (await posts.GetAsync(Fixture015.DonePost, Ct))!.Status.ShouldBe(PostStatus.Done);
        }
        finally
        {
            var trigger = new WindowsTaskTrigger(paths, new ProcessCommandRunner(), NullLogger<WindowsTaskTrigger>.Instance);
            (await trigger.RemoveAsync(task, CancellationToken.None)).State.ShouldBe(TriggerState.Off);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The 0.1.5 database with its key sealed for this user (as the window leaves it), the scheduled post an hour late and the draft left
    /// "publishing" by a sender that ended. Returns a factory on the open database for the checks after the run.
    /// </summary>
    private static async Task<PublisherDbContextFactory> PrepareAsync(AppPaths paths)
    {
        File.Copy(Fixture015.File, paths.DatabaseFile);
        var protector = new DpapiKeyProtector();
        KeyFile.Write(paths.KeyFile, await protector.ProtectAsync(Fixture015.Key, Ct));
        var key = new DatabaseKey();
        await new Vault(paths, key, [protector], NullLogger<Vault>.Instance).OpenAsync(Ct);
        var factory = new PublisherDbContextFactory(paths, key);

        var posts = new PostStore(factory, TimeProvider.System);
        var late = (await posts.GetAsync(Fixture015.ScheduledPost, Ct))!;
        late.ScheduledAtUtc = DateTimeOffset.UtcNow.AddHours(-1);
        await posts.SaveAsync(late, Ct);
        var interrupted = (await posts.GetAsync(Fixture015.DraftPost, Ct))!;
        interrupted.Status = PostStatus.Publishing;
        await posts.SaveAsync(interrupted, Ct);
        return factory;
    }
}
