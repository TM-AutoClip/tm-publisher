using System.Text.RegularExpressions;

namespace TMPublisher.Core.Platforms;

/// <summary>Estimated API cost of one post on a pay-per-use platform (X): the link price when the text has an address.</summary>
public static partial class CostEstimator
{
    /// <summary>Estimated cost in US dollars (0 for free platforms).</summary>
    public static decimal Estimate(PlatformCapabilities capabilities, string? text)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return HasLink(text) && capabilities.CostPerLinkPost > 0 ? capabilities.CostPerLinkPost : capabilities.CostPerPost;
    }

    /// <summary>Whether the text contains a web address.</summary>
    public static bool HasLink(string? text) => LinkPattern().IsMatch(text ?? string.Empty);

    [GeneratedRegex(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}
