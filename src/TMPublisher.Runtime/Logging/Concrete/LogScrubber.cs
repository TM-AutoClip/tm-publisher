using System.Text.RegularExpressions;
using TMPublisher.Core.Security;

namespace TMPublisher.Runtime.Logging;

/// <summary>
/// The last check before a log line reaches a file (and again before a file goes into a report zip). Code never logs personal data or secrets
/// on purpose; this masks what slips through anyway: the user's home folder (it carries the user name) becomes <c>~</c>, e-mail addresses
/// become <c>***@***</c>, and tokens (Bearer / Basic, secret query and JSON values, webhook and bot addresses, common API key shapes, JWTs)
/// become <c>***</c>.
/// </summary>
public static partial class LogScrubber
{
    /// <summary>The home folder of the user running this process.</summary>
    public static string Home { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The masked text.</summary>
    public static string Scrub(string? text) => Scrub(text, Home);

    /// <summary>The masked text, with <paramref name="home"/> as the home folder.</summary>
    public static string Scrub(string? text, string? home)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;
        if (!string.IsNullOrEmpty(home) && home.Length > 3)
        {
            var trimmed = Path.TrimEndingDirectorySeparator(home);
            result = ReplacePath(result, trimmed);
            result = ReplacePath(result, trimmed.Replace('\\', '/'));
            result = ReplacePath(result, trimmed.Replace('/', '\\'));
        }

        result = Email().Replace(result, "***@***");
        result = Redactor.Redact(result);
        result = ApiKey().Replace(result, "***");
        result = Jwt().Replace(result, "***");
        return result;
    }

    private static string ReplacePath(string text, string home) =>
        text.Replace(home, "~", OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    // OpenAI / Anthropic style (sk-, sk-ant-), Google (AIza), GitHub (ghp_, gho_, ghu_, ghs_, ghr_, github_pat_), Slack (xox?-).
    [GeneratedRegex(@"\b(?:sk-[A-Za-z0-9_-]{16,}|AIza[0-9A-Za-z_-]{30,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[abprs]-[A-Za-z0-9-]{10,})")]
    private static partial Regex ApiKey();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}")]
    private static partial Regex Jwt();
}
