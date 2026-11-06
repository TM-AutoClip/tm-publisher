using System.Text;
using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Paths;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// Windows: a Task Scheduler task of the current user (<see cref="WindowsTaskXml"/>), written and removed with <c>schtasks.exe</c> (no
/// administrator rights; the task runs only while the user is signed in). The XML goes through a file in the data folder that is removed at once.
/// </summary>
public sealed class WindowsTaskTrigger(AppPaths paths, ICommandRunner runner, ILogger<WindowsTaskTrigger> logger) : ISystemTrigger
{
    /// <summary>The user the task runs as (the current user's SID; replaced in tests).</summary>
    internal Func<string> UserId { get; set; } = CurrentUser;

    private static string Schtasks => Path.Combine(Environment.SystemDirectory, "schtasks.exe");

    /// <inheritdoc />
    public string Kind => "Task Scheduler";

    /// <inheritdoc />
    public string Describe(TriggerPlan plan) => WindowsTaskXml.Build(plan, UserId());

    /// <inheritdoc />
    public async Task<TriggerResult> ApplyAsync(TriggerPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var file = Path.Combine(paths.DataDirectory, "system-trigger.xml");
        try
        {
            await File.WriteAllTextAsync(file, Describe(plan), Encoding.Unicode, cancellationToken).ConfigureAwait(false);
            var (code, output, error) = await runner.RunAsync(Schtasks, ["/Create", "/TN", plan.Name, "/XML", file, "/F"], null, cancellationToken)
                .ConfigureAwait(false);
            if (code != 0)
            {
                var reason = Reason(code, output, error);
                logger.LogError("The Task Scheduler task could not be written: {Reason}", reason);
                return new TriggerResult(TriggerState.Failed, reason);
            }

            return new TriggerResult(TriggerState.On, string.Empty);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <inheritdoc />
    public async Task<TriggerResult> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return new TriggerResult(TriggerState.Off, string.Empty);
        }

        var (code, output, error) = await runner.RunAsync(Schtasks, ["/Delete", "/TN", name, "/F"], null, cancellationToken).ConfigureAwait(false);
        if (code != 0)
        {
            var reason = Reason(code, output, error);
            logger.LogError("The Task Scheduler task could not be removed: {Reason}", reason);
            return new TriggerResult(TriggerState.Failed, reason);
        }

        return new TriggerResult(TriggerState.Off, string.Empty);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) =>
        (await runner.RunAsync(Schtasks, ["/Query", "/TN", name], null, cancellationToken).ConfigureAwait(false)).ExitCode == 0;

    private static string Reason(int code, string output, string error) =>
        "schtasks exit code " + code + ": " + LogScrubber.Scrub((error.Length > 0 ? error : output).Trim());

    private static string CurrentUser() =>
        OperatingSystem.IsWindows()
            ? System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserDomainName + "\\" + Environment.UserName
            : Environment.UserName;
}
