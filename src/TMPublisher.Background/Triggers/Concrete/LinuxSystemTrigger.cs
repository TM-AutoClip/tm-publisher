using Microsoft.Extensions.Logging;
using TMPublisher.Runtime.Logging;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// Linux: a systemd user timer and service (<see cref="SystemdUnits"/>, in <c>$XDG_CONFIG_HOME/systemd/user</c>), enabled with
/// <c>systemctl --user</c> (no administrator rights). When there is no systemd user session, an XDG autostart entry instead
/// (<see cref="XdgAutostartEntry"/>, sign-in only) and a warning.
/// </summary>
public sealed class LinuxSystemTrigger(ICommandRunner runner, XdgAutostartEntry autostart, ILogger<LinuxSystemTrigger> logger) : ISystemTrigger
{
    /// <summary>The warning of the fallback (English, for the log and the record; Settings shows its own text).</summary>
    public const string FallbackWarning =
        "No systemd user session: posts are sent only while the TM Publisher window is open; a post whose time passes while it is closed is not sent and waits for the user.";

    private bool? _systemd;

    /// <inheritdoc />
    public string Kind => "systemd user timer";

    private static string UnitFolder => Path.Combine(XdgAutostartEntry.ConfigFolder, "systemd", "user");

    /// <inheritdoc />
    public string Describe(TriggerPlan plan) => SystemdUnits.Service(plan) + "\n" + (SystemdUnits.Timer(plan) ?? "(no timer: nothing scheduled)");

    /// <inheritdoc />
    public async Task<TriggerResult> ApplyAsync(TriggerPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!await SystemdAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            autostart.Write(plan);
            logger.LogWarning("{Warning}", FallbackWarning);
            return new TriggerResult(TriggerState.SignInOnly, FallbackWarning);
        }

        autostart.Remove(plan.Name);
        Directory.CreateDirectory(UnitFolder);
        var timerFile = Path.Combine(UnitFolder, plan.Name + ".timer");
        await File.WriteAllTextAsync(Path.Combine(UnitFolder, plan.Name + ".service"), SystemdUnits.Service(plan), cancellationToken).ConfigureAwait(false);
        string[][] steps;
        if (SystemdUnits.Timer(plan) is { } timer)
        {
            await File.WriteAllTextAsync(timerFile, timer, cancellationToken).ConfigureAwait(false);
            steps = [["daemon-reload"], ["enable", plan.Name + ".timer"], ["restart", plan.Name + ".timer"]];
        }
        else
        {
            // Nothing scheduled: no timer until a post is (the window writes it then).
            if (File.Exists(timerFile))
            {
                await SystemctlAsync(["disable", "--now", plan.Name + ".timer"], cancellationToken).ConfigureAwait(false);
                File.Delete(timerFile);
            }

            steps = [["daemon-reload"]];
        }

        foreach (var step in steps)
        {
            if (await SystemctlAsync(step, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                logger.LogError("The systemd user timer could not be set ({Step}): {Reason}", step[0], failure);
                return new TriggerResult(TriggerState.Failed, failure);
            }
        }

        return new TriggerResult(TriggerState.On, string.Empty);
    }

    /// <inheritdoc />
    public async Task<TriggerResult> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        autostart.Remove(name);
        var timer = Path.Combine(UnitFolder, name + ".timer");
        var service = Path.Combine(UnitFolder, name + ".service");
        if (!File.Exists(timer) && !File.Exists(service))
        {
            return new TriggerResult(TriggerState.Off, string.Empty);
        }

        if (await SystemdAvailableAsync(cancellationToken).ConfigureAwait(false)
            && await SystemctlAsync(["disable", "--now", name + ".timer"], cancellationToken).ConfigureAwait(false) is { } failure)
        {
            logger.LogWarning("The systemd user timer could not be stopped: {Reason}", failure);
        }

        File.Delete(timer);
        File.Delete(service);
        if (await SystemdAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await SystemctlAsync(["daemon-reload"], cancellationToken).ConfigureAwait(false);
        }

        return new TriggerResult(TriggerState.Off, string.Empty);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(Path.Combine(UnitFolder, name + ".service")) || XdgAutostartEntry.Exists(name));

    private async Task<bool> SystemdAvailableAsync(CancellationToken cancellationToken)
    {
        if (_systemd is { } known)
        {
            return known;
        }

        try
        {
            _systemd = runner.Exists("systemctl")
                && (await runner.RunAsync("systemctl", ["--user", "show-environment"], null, cancellationToken).ConfigureAwait(false)).ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            logger.LogWarning("systemctl could not be run ({Type}): {Reason}", ex.GetType().Name, ex.Message);
            _systemd = false;
        }

        return _systemd.Value;
    }

    /// <summary>Runs <c>systemctl --user</c>; the reason of a failure, <c>null</c> when it worked.</summary>
    private async Task<string?> SystemctlAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var (code, output, error) = await runner.RunAsync("systemctl", ["--user", .. arguments], null, cancellationToken).ConfigureAwait(false);
        return code == 0 ? null : "systemctl exit code " + code + ": " + LogScrubber.Scrub((error.Length > 0 ? error : output).Trim());
    }
}
