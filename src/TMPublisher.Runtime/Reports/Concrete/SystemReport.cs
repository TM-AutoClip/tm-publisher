using System.Text;
using TMPublisher.Runtime.Logging;

namespace TMPublisher.Runtime.Reports;

/// <summary>
/// <c>system.txt</c> of a problem report: the program, its version and commit, the operating system, the processor architecture, the .NET
/// version and the few facts the caller adds (interface language, drawing mode, which optional programs are there). Never settings values,
/// keys, database content or account names.
/// </summary>
public static class SystemReport
{
    /// <summary>The text.</summary>
    public static string Text(string program, IEnumerable<KeyValuePair<string, string>> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var text = new StringBuilder()
            .Append("Program: ").AppendLine(program)
            .Append("Version: ").AppendLine(LogHeader.Version)
            .Append("Operating system: ").AppendLine(LogHeader.OperatingSystem)
            .Append("Architecture: ").AppendLine(LogHeader.Architecture)
            .Append(".NET: ").AppendLine(LogHeader.Framework);
        foreach (var (name, value) in facts)
        {
            text.Append(name).Append(": ").AppendLine(value);
        }

        return LogScrubber.Scrub(text.ToString());
    }
}
