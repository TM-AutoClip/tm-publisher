namespace TMPublisher.Desktop.Reports;

/// <summary>Where a problem is reported and how the report files are named.</summary>
public static class ReportLinks
{
    /// <summary>The GitHub repository (<c>owner/name</c>).</summary>
    public const string Repository = "TM-AutoClip/tm-publisher";

    /// <summary>The issue form (<c>.github/ISSUE_TEMPLATE/bug_report.yml</c>).</summary>
    public const string Template = "bug_report.yml";

    /// <summary>The start of the logs zip's name (<c>tm-publisher-logs-yyyyMMdd-HHmm.zip</c>).</summary>
    public const string ZipName = "tm-publisher";

    /// <summary>Field id of the version in the issue form.</summary>
    public const string VersionField = "version";

    /// <summary>Field id of the operating system in the issue form.</summary>
    public const string OsField = "os";

    /// <summary>Field id of the processor architecture in the issue form.</summary>
    public const string ArchitectureField = "architecture";
}
