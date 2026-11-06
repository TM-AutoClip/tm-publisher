namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The process kinds; each writes its own daily file (<c>logs/&lt;kind&gt;-yyyy-MM-dd.log</c>): two processes appending to one file would
/// interleave or overwrite each other's lines.
/// </summary>
public static class LogKinds
{
    /// <summary>The window program.</summary>
    public const string App = "app";

    /// <summary>The background scheduler.</summary>
    public const string Scheduler = "scheduler";

    /// <summary>The short-lived notification process (<c>--notify</c>).</summary>
    public const string Notify = "notify";

    /// <summary>Every kind (retention and the logs zip only touch files of these names).</summary>
    public static readonly IReadOnlyList<string> All = [App, Scheduler, Notify];
}
