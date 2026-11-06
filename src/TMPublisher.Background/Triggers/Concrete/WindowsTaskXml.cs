using System.Globalization;
using System.Xml.Linq;

namespace TMPublisher.Background.Triggers;

/// <summary>
/// The Task Scheduler definition (task XML, schema 1.2) of a <see cref="TriggerPlan"/>, for the current user without administrator rights:
/// runs only while the user is signed in (interactive token, least privilege), a one-time start at the next post's time (UTC), a start one
/// minute after the user signs in, and, while a post is scheduled, a start every 30 minutes from 30 minutes after that time (a run that ended
/// before it set the next start). Missed starts run as soon as possible (the computer was off or asleep), a second start while one runs is
/// ignored, a run longer than an hour is stopped, the computer is woken for it only when the user asked, and battery power does not stop it.
/// </summary>
public static class WindowsTaskXml
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>The XML text (UTF-16 declaration: schtasks reads the file as UTF-16).</summary>
    public static string Build(TriggerPlan plan, string userId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        var triggers = new XElement(Ns + "Triggers");
        if (plan.NextUtc is { } next)
        {
            triggers.Add(new XElement(Ns + "TimeTrigger",
                new XElement(Ns + "StartBoundary", Utc(next)),
                new XElement(Ns + "Enabled", "true")));
            if (plan.Safety)
            {
                triggers.Add(new XElement(Ns + "TimeTrigger",
                    new XElement(Ns + "Repetition",
                        new XElement(Ns + "Interval", Duration(TriggerLimits.SafetyInterval)),
                        new XElement(Ns + "StopAtDurationEnd", "false")),
                    new XElement(Ns + "StartBoundary", Utc(next + TriggerLimits.SafetyInterval)),
                    new XElement(Ns + "Enabled", "true")));
            }
        }

        triggers.Add(new XElement(Ns + "LogonTrigger",
            new XElement(Ns + "Enabled", "true"),
            new XElement(Ns + "UserId", userId),
            new XElement(Ns + "Delay", Duration(TriggerLimits.SignInDelay))));

        var task = new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo",
                new XElement(Ns + "Author", "TM Publisher"),
                new XElement(Ns + "Description",
                    "Starts TM Publisher for a moment while it is closed, to send the scheduled posts that are due; it ends right after. "
                    + "Turn it off in TM Publisher: Settings, \"Send scheduled posts while TM Publisher is closed\"."),
                new XElement(Ns + "URI", "\\" + plan.Name)),
            triggers,
            new XElement(Ns + "Principals",
                new XElement(Ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(Ns + "UserId", userId),
                    new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "LeastPrivilege"))),
            new XElement(Ns + "Settings",
                new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                new XElement(Ns + "AllowHardTerminate", "true"),
                new XElement(Ns + "StartWhenAvailable", "true"),
                new XElement(Ns + "RunOnlyIfNetworkAvailable", "false"),
                new XElement(Ns + "IdleSettings",
                    new XElement(Ns + "StopOnIdleEnd", "false"),
                    new XElement(Ns + "RestartOnIdle", "false")),
                new XElement(Ns + "AllowStartOnDemand", "true"),
                new XElement(Ns + "Enabled", "true"),
                new XElement(Ns + "Hidden", "false"),
                new XElement(Ns + "RunOnlyIfIdle", "false"),
                new XElement(Ns + "WakeToRun", plan.WakeToRun ? "true" : "false"),
                new XElement(Ns + "ExecutionTimeLimit", Duration(TriggerLimits.RunTimeLimit)),
                new XElement(Ns + "Priority", "7")),
            new XElement(Ns + "Actions", new XAttribute("Context", "Author"),
                new XElement(Ns + "Exec",
                    new XElement(Ns + "Command", plan.Program),
                    new XElement(Ns + "Arguments", Arguments(plan.Arguments)))));
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" + task.ToString(SaveOptions.None).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>The arguments as Windows parses a command line: a part with a space or a quote is quoted (a trailing backslash doubled).</summary>
    internal static string Arguments(IReadOnlyList<string> arguments) => string.Join(' ', arguments.Select(Quote));

    private static string Quote(string part) =>
        part.Length > 0 && !part.Contains(' ', StringComparison.Ordinal) && !part.Contains('"', StringComparison.Ordinal)
            ? part
            : "\"" + part.Replace("\"", "\\\"", StringComparison.Ordinal) + (part.EndsWith('\\') ? "\\" : string.Empty) + "\"";

    private static string Utc(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Duration(TimeSpan span) => System.Xml.XmlConvert.ToString(span);
}
