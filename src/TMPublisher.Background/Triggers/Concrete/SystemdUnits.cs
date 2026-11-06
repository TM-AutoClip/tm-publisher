using System.Globalization;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// The systemd user units of a <see cref="TriggerPlan"/>: a one-shot service that runs the program with <c>--run-due</c> (stopped after an hour;
/// <c>KillMode=process</c>, so the notification process the run started outlives it),
/// and its timer, only with calendar times (UTC): the next post's time and the safety starts 30, 60 and 120 minutes after it. With
/// <c>Persistent=true</c> a start missed while the computer was off, asleep or nobody was signed in runs as soon as the timer is back (at
/// sign-in). No "after boot" or "after the last start" times: such a timer fires at once each time it is written again. Nothing scheduled:
/// no timer (<see cref="Timer"/> is <c>null</c>).
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

            // Only the run itself is stopped when it ends: the notification process it started (--notify) stays until its last notification
            // closed. With the default (control-group) systemd would end it together with the run, before the notification is seen.
            "KillMode=process",
            "ExecStart=" + string.Join(' ', new[] { plan.Program }.Concat(plan.Arguments).Select(Quote)),
            "TimeoutStartSec=" + Minutes(TriggerLimits.RunTimeLimit),
            string.Empty);
    }

    /// <summary>The timer unit text; <c>null</c> when nothing is scheduled (a timer needs at least one time).</summary>
    public static string? Timer(TriggerPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.NextUtc is not { } next)
        {
            return null;
        }

        var lines = new List<string>
        {
            "# Written by TM Publisher. Turn it off in TM Publisher: Settings, \"Send scheduled posts while TM Publisher is closed\".",
            "[Unit]",
            "Description=TM Publisher: start at the next scheduled post's time",
            string.Empty,
            "[Timer]",
        };
        lines.Add("OnCalendar=" + Calendar(next));
        if (plan.Safety)
        {
            lines.AddRange(TriggerLimits.SafetyStarts.Select(after => "OnCalendar=" + Calendar(next + after)));
        }

        lines.Add("Persistent=true");
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

    private static string Calendar(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

    private static string Minutes(TimeSpan span) => ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "min";
}
