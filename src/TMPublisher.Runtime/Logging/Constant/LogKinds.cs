namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The process kinds; each writes its own daily file (<c>logs/&lt;kind&gt;-yyyy-MM-dd.log</c>): two processes appending to one file would
/// interleave or overwrite each other's lines.
/// </summary>
public static class LogKinds
{
    /// <summary>The window.</summary>
    public const string App = "app";

    /// <summary>A short run the system scheduler starts (<c>--run-due</c>): it sends what is due and ends.</summary>
    public const string Run = "run";

    /// <summary>The short-lived notification process (<c>--notify</c>).</summary>
    public const string Notify = "notify";

    /// <summary>The always-running scheduler of 0.1.3–0.1.4; its files are still cleaned after 7 days and go into the logs zip.</summary>
    public const string Scheduler = "scheduler";

    /// <summary>Every kind (retention and the logs zip only touch files of these names).</summary>
    public static readonly IReadOnlyList<string> All = [App, Run, Notify, Scheduler];
}
