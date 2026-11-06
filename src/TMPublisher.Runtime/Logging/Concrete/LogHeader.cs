using System.Reflection;
using System.Runtime.InteropServices;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The first line of every log file and of every start: which program, version and commit, operating system, processor architecture and .NET
/// version wrote the lines below it (nothing about the user).
/// </summary>
public static class LogHeader
{
    /// <summary>Version with the commit it was built from (<c>0.1.4+abc1234</c>), from the informational version.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Operating system description (e.g. <c>Microsoft Windows 10.0.26200</c>).</summary>
    public static string OperatingSystem => RuntimeInformation.OSDescription;

    /// <summary>Processor architecture of the process (<c>X64</c>, <c>Arm64</c>).</summary>
    public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString();

    /// <summary>.NET version (e.g. <c>.NET 10.0.0</c>).</summary>
    public static string Framework => RuntimeInformation.FrameworkDescription;

    /// <summary>The header line for <paramref name="program"/> (e.g. <c>TM Publisher scheduler</c>).</summary>
    public static string Line(string program) =>
        "=== " + program + " " + Version + " | " + OperatingSystem + " | " + Architecture + " | " + Framework + " ===";

    private static string ReadVersion()
    {
        var assembly = typeof(LogHeader).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // The SDK appends the full commit id (40 characters); the short one is enough to find it.
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 && informational.Length > plus + 8 ? informational[..(plus + 8)] : informational;
    }
}
