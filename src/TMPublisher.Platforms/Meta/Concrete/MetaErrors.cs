using TMPublisher.Core.Publishing;
using TMPublisher.Platforms.Common;

namespace TMPublisher.Platforms.Meta;

/// <summary>Meta error bodies (<c>error.code</c>, <c>error.message</c>): 190 / 102 = token, rate-limit codes = quota.</summary>
public static class MetaErrors
{
    /// <summary>The exception of a failed answer.</summary>
    public static Exception Classify(string platform, string step, int status, string body)
    {
        var error = JsonSafe.TryParse(body)?["error"];
        var code = JsonSafe.Str(error, "code");
        var message = JsonSafe.Str(error, "message") is { Length: > 0 } m ? m : PlatformText.Short(body);
        var text = $"{platform} {step} failed (HTTP {status}, code {(code.Length > 0 ? code : "?")}): {PlatformText.Short(message)}";
        return code switch
        {
            "190" or "102" => new PlatformAuthException(text + " The access was revoked or expired; connect the account again."),
            "4" or "17" or "32" or "613" or "9" => new PlatformQuotaException(text),
            _ when status == 401 => new PlatformAuthException(text),
            _ => new PlatformRejectedException(text),
        };
    }
}
