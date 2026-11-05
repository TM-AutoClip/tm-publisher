using System.IO.Pipes;
using Shouldly;
using TMPublisher.Core.Notifications;
using TMPublisher.Runtime.Instance;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Notifications;
using TMPublisher.Tests.Storage;

namespace TMPublisher.Tests.Runtime;

/// <summary>The local channel between the window and the background scheduler, and the per-folder instance lock.</summary>
public sealed class ChannelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARequestGetsItsAnswerLine()
    {
        using var paths = new TempPaths();
        var client = new SchedulerClient(paths.DataDirectory);
        await using var server = new NamedPipeServerStream(client.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, SchedulerChannel.Options);
        var serve = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(Ct);
            var request = await SchedulerChannel.ReadLineAsync(server, Ct);
            await SchedulerChannel.WriteLineAsync(server, request == "key c2VjcmV0" ? SchedulerCommands.Ok : "unexpected " + request, Ct);
        }, Ct);

        var reply = await client.SendAsync(SchedulerCommands.Key, "c2VjcmV0", Ct);
        await serve;
        reply.Reached.ShouldBeTrue();
        reply.IsOk.ShouldBeTrue();
    }

    [Fact]
    public async Task NoSchedulerMeansNotRunningWithAReason()
    {
        using var paths = new TempPaths();
        var state = await new SchedulerClient(paths.DataDirectory).StatusAsync(Ct);
        state.Status.ShouldBe(SchedulerStatus.NotRunning);
        state.Message.ShouldNotBeEmpty();
    }

    [Fact]
    public void NotificationsAndStatesRoundTrip()
    {
        var message = new NotificationMessage(NotificationKind.Failed, "Başarısız", "X · hesap: \"quote\"\nline", 60);
        SchedulerChannel.ParseNotification(SchedulerChannel.NotifyLine(message)).ShouldBe(message);
        SchedulerChannel.ParseNotification(SchedulerChannel.NotificationJson(message)).ShouldBe(message);
        SchedulerChannel.NotifyLine(message).ShouldNotContain("\n");
        var state = SchedulerChannel.ParseState(SchedulerChannel.StateJson(new SchedulerState { Status = SchedulerStatus.Paused, Version = "0.1.3" }))!;
        state.Status.ShouldBe(SchedulerStatus.Paused);
        SchedulerChannel.ParseState("ok").ShouldBeNull();
    }

    [Fact]
    public void OnlyOneHolderPerDataFolder()
    {
        using var paths = new TempPaths();
        using (var first = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Scheduler, out _))
        {
            first.ShouldNotBeNull();
            InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Scheduler, out var reason).ShouldBeNull();
            reason.ShouldNotBeEmpty();
            using var window = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Window, out _);
            window.ShouldNotBeNull();
        }

        using var again = InstanceLock.TryAcquire(paths.DataDirectory, InstanceLock.Scheduler, out _);
        again.ShouldNotBeNull();
    }
}
