using System.Globalization;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// The systemd user units of a <see cref="TriggerPlan"/>: a one-shot service that runs the program with <c>--run-due</c> (stopped after an hour),
/// and its timer: at the next post's time (UTC; <c>Persistent=true</c> runs a start missed while the computer was off or asleep as soon as it
/// is back), one minute after the user's service manager starts (sign-in), and, while a post is scheduled, 30 minutes after each run (a run
/// that ended before it set the next start).
/// </summary>
public static class SystemdUnits
{
    /// <summary>The service unit text.</summary>
    public static string Service(TriggerPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return string.Join('\n',
            "# Written by TM Publisher. Turn it off in TM Publisher: Settings, \"Send scheduled posts while TM Publisher is closed\".",
            "[Unit]",
            "Description=TM Publisher: send the scheduled posts that are due",
            string.Empty,
            "[Service]",
            "Type=oneshot",
            "ExecStart=" + string.Join(' ', new[] { plan.Program }.Concat(plan.Arguments).Select(Quote)),
            "TimeoutStartSec=" + Minutes(TriggerLimits.RunTimeLimit),
            string.Empty);
    }

    /// <summary>The timer unit text.</summary>
    public static string Timer(TriggerPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var lines = new List<string>
        {
            "# Written by TM Publisher. Turn it off in TM Publisher: Settings, \"Send scheduled posts while TM Publisher is closed\".",
            "[Unit]",
            "Description=TM Publisher: start at the next scheduled post's time",
            string.Empty,
            "[Timer]",
        };
        if (plan.NextUtc is { } next)
        {
            lines.Add("OnCalendar=" + next.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC");
            lines.Add("Persistent=true");
            if (plan.Safety)
            {
                lines.Add("OnUnitActiveSec=" + Minutes(TriggerLimits.SafetyInterval));
            }
        }

        lines.Add("OnStartupSec=" + Minutes(TriggerLimits.SignInDelay));
        lines.Add("AccuracySec=1s");
        lines.Add("Unit=" + plan.Name + ".service");
        lines.Add(string.Empty);
        lines.Add("[Install]");
        lines.Add("WantedBy=timers.target");
        lines.Add(string.Empty);
        return string.Join('\n', lines);
    }

    /// <summary>
    /// One word of <c>ExecStart=</c>: double-quoted when it has a space or a quote (backslash and quote escaped), <c>%</c> as <c>%%</c>
    /// (specifiers) and <c>$</c> as <c>$$</c> (variables).
    /// </summary>
    internal static string Quote(string part)
    {
        var escaped = part.Replace("%", "%%", StringComparison.Ordinal).Replace("$", "$$", StringComparison.Ordinal);
        return escaped.Length > 0 && !escaped.Any(c => c is ' ' or '"' or '\'' or '\\' or '\t')
            ? escaped
            : "\"" + escaped.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string Minutes(TimeSpan span) => ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "min";
}
