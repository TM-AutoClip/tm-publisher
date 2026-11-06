using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Background.Sending;
using TMPublisher.Background.Triggers;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Background;

/// <summary>
/// The real operating system, only under test names that are removed again: a Task Scheduler task (Windows), the run as a process of the
/// built program, and a stand-in for a 0.1.3–0.1.4 scheduler that is asked to end. No developer's task, autostart entry or data is touched.
/// </summary>
public sealed class SystemIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ATaskSchedulerTaskOfATestNameIsWrittenAndRemoved()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Task Scheduler is Windows only");
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "task-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(folder);
        var trigger = new WindowsTaskTrigger(paths, new ProcessCommandRunner(), NullLogger<WindowsTaskTrigger>.Instance);
        var name = "TMPublisherTest " + Guid.NewGuid().ToString("N")[..8];
        var plan = new TriggerPlan(name, Path.Combine(folder, "none", "TMPublisher.exe"), ["--run-due", "--data-dir", folder],
            DateTimeOffset.UtcNow.AddDays(2), Safety: true, WakeToRun: false);
        try
        {
            (await trigger.ApplyAsync(plan, Ct)).State.ShouldBe(TriggerState.On);
            (await trigger.ExistsAsync(name, Ct)).ShouldBeTrue();
            (await trigger.ApplyAsync(plan with { NextUtc = DateTimeOffset.UtcNow.AddDays(3) }, Ct)).State.ShouldBe(TriggerState.On);
        }
        finally
        {
            (await trigger.RemoveAsync(name, CancellationToken.None)).State.ShouldBe(TriggerState.Off);
            Directory.Delete(folder, recursive: true);
        }

        (await trigger.ExistsAsync(name, Ct)).ShouldBeFalse();
        File.Exists(Path.Combine(folder, "system-trigger.xml")).ShouldBeFalse();
    }

    [Fact]
    public async Task TheRunAsAProcessLoadsNoUiLibraryAndEnds()
    {
        var program = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "TMPublisher.exe" : "TMPublisher");
        Assert.SkipUnless(File.Exists(program), "the program file is not next to the tests");
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var start = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--run-due");
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(folder);
            start.Environment[TriggerNames.PrefixVariable] = "TMPublisherTest";
            using var process = Process.Start(start)!;
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct))
            {
                limit.CancelAfter(TimeSpan.FromMinutes(1));
                await process.WaitForExitAsync(limit.Token);
            }

            process.ExitCode.ShouldBe(0);
            var log = File.ReadAllText(Directory.GetFiles(Path.Combine(folder, "logs"), "run-*.log").Single());
            log.ShouldContain("No database yet");
            log.ShouldContain("UI library loaded: no");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AnOldSchedulerIsAskedToEndAndTheRunTakesOver()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "old-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(folder);
        try
        {
            (await OldScheduler.AskToQuitAsync(folder, Ct)).ShouldBe(OldSchedulerAnswer.NotRunning);

            // A stand-in for the 0.1.3-0.1.4 scheduler: it holds the lock and ends (gives it back) when asked to quit.
            var held = InstanceLock.TryAcquire(folder, InstanceLock.Sender, out _)!;
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            var old = Task.Run(async () =>
            {
                var server = new NamedPipeServerStream(OldScheduler.PipeName(folder), PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await using (server)
                {
                    await server.WaitForConnectionAsync(stop.Token);
                    var buffer = new byte[16];
                    var read = await server.ReadAsync(buffer, stop.Token);
                    System.Text.Encoding.UTF8.GetString(buffer, 0, read).ShouldBe("quit\n");
                    await server.WriteAsync("ok\n"u8.ToArray(), stop.Token);
                    await server.FlushAsync(stop.Token);
                    held.Dispose();
                }
            }, Ct);

            using var gate = new SenderGate(paths, TimeProvider.System, NullLogger<SenderGate>.Instance);
            (await gate.TryTakeAsync(TimeSpan.FromSeconds(20), Ct)).ShouldBeTrue();
            await old;
            gate.IsHeld.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
