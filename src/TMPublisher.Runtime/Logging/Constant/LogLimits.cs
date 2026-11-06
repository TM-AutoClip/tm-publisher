namespace TMPublisher.Runtime.Logging;

/// <summary>How much log is kept.</summary>
public static class LogLimits
{
    /// <summary>Days of log files kept, today included: a file dated 7 or more days ago is deleted.</summary>
    public const int KeepDays = 7;

    /// <summary>Bytes one file may reach in a day; after that one warning line and nothing more until the next day.</summary>
    public const long DailyBytes = 20L * 1024 * 1024;

    /// <summary>Report zips kept in <c>reports/</c> (the newest; older ones are deleted when a new one is made).</summary>
    public const int KeptReports = 5;
}
