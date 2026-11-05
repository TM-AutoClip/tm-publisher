using System.Text.RegularExpressions;

namespace TMPublisher.Core.Security;

/// <summary>
/// Safety net for logs: masks anything that looks like a token, a secret query value or a bearer header. Code never logs secrets on
/// purpose; this catches what a platform echoes back in an error body.
/// </summary>
public static partial class Redactor
{
    /// <summary>The text with secrets replaced by <c>***</c>.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = QueryValue().Replace(text, m => m.Groups[1].Value + "=***");
        result = Bearer().Replace(result, m => m.Groups[1].Value + " ***");
        result = JsonValue().Replace(result, m => m.Groups[1].Value + "\"***\"");
        result = Webhook().Replace(result, "discord.com/api/webhooks/***");
        result = BotPath().Replace(result, "/bot***/");
        return result;
    }

    [GeneratedRegex(@"\b((?:access_token|refresh_token|client_secret|code|password|token|fb_exchange_token|th_exchange_token|key)\s*)=[^&\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex QueryValue();

    [GeneratedRegex(@"\b(Bearer|OAuth|Basic)\s+[A-Za-z0-9._~+/=-]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"(""(?:access_token|refresh_token|accessJwt|refreshJwt|client_secret|password|token|id_token)""\s*:\s*)""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex JsonValue();

    [GeneratedRegex(@"discord(?:app)?\.com/api/webhooks/[^\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex Webhook();

    [GeneratedRegex(@"/bot[0-9]+:[A-Za-z0-9_-]+/")]
    private static partial Regex BotPath();
}
