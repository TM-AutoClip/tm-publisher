using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Core.Instance;
using TMPublisher.Desktop.Scheduler;
using TMPublisher.Desktop.Startup;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Storage.Keys;
using TMPublisher.Tests.Scheduling;

namespace TMPublisher.Tests.Desktop;

/// <summary>The window's side of the background scheduler: starting it, handing over the database and its key.</summary>
public sealed class SchedulerSupervisorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSchedulerIsNotStartedNextToAnOldVersion()
    {
        var old = new FakeOldVersion { Runs = true };
        using var h = new DesktopHarness(s => s.AddSingleton<IOldVersionCheck>(old));
        var supervisor = h.Services.GetRequiredService<SchedulerSupervisor>();

        var state = await supervisor.EnsureRunningAsync(Ct);

        h.Launcher.Starts.ShouldBe(0);
        state.Status.ShouldBe(SchedulerStatus.NotRunning);
        state.Message.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AfterOpeningTheNewDatabaseTheMasterPasswordKeyIsHandedOver()
    {
        using var h = new DesktopHarness();
        h.Services.GetRequiredService<UiMasterPassword>().Provide("a long master password");
        var vault = h.Services.GetRequiredService<Vault>();
        await vault.CreateAsync(vault.PasswordProtector, Ct);
        var supervisor = h.Services.GetRequiredService<SchedulerSupervisor>();

        // A scheduler that ran without a database: "reload" makes it find the password key file and wait for the key.
        var version = typeof(SchedulerSupervisor).Assembly.GetName().Version!.ToString(3);
        var status = SchedulerStatus.NoDatabase;
        var commands = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var pipe = new SchedulerClient(h.Paths.DataDirectory).PipeName;
        var serve = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, SchedulerChannel.Options);
                try
                {
                    await server.WaitForConnectionAsync(stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var command = (await SchedulerChannel.ReadLineAsync(server, stop.Token) ?? string.Empty).Split(' ')[0];
                commands.Add(command);
                status = command switch
                {
                    SchedulerCommands.Reload => SchedulerStatus.WaitingForKey,
                    SchedulerCommands.Key => SchedulerStatus.Running,
                    _ => status,
                };
                await SchedulerChannel.WriteLineAsync(server, command == SchedulerCommands.Status
                    ? SchedulerChannel.StateJson(new SchedulerState { Version = version, Status = status })
                    : SchedulerCommands.Ok, stop.Token);
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        server.WaitForPipeDrain();
                    }
                    catch (IOException)
                    {
                        // The client already read the answer and closed.
                    }
                }
            }
        }, Ct);

        var state = await supervisor.EnsureRunningAsync(Ct);
        await stop.CancelAsync();
        await serve;

        state.Status.ShouldBe(SchedulerStatus.Running);
        commands.ShouldBe([SchedulerCommands.Status, SchedulerCommands.Reload, SchedulerCommands.Status, SchedulerCommands.Key, SchedulerCommands.Status]);
        h.Launcher.Starts.ShouldBe(0);
    }
}
