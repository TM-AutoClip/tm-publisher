using TMPublisher.Core.Security;

namespace TMPublisher.Platforms.Common;

/// <summary>Text helpers for error messages: short and without secrets.</summary>
public static class PlatformText
{
    /// <summary>The first 300 characters of a body, redacted.</summary>
    public static string Short(string? body)
    {
        var text = Redactor.Redact((body ?? string.Empty).Trim());
        return text.Length <= 300 ? text : text[..300] + "…";
    }
}
