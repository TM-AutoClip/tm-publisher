using System.Collections.Concurrent;

namespace TMPublisher.Core.Security;

/// <summary>
/// Server names this process has talked to that belong to the user's own account (a Mastodon instance, a Bluesky server they chose: the name
/// may be their own domain). Whoever reaches such a server registers it here; the log masker replaces every registered name with
/// <see cref="Masked"/> before a line reaches a file, so an exception message that carries the name (".NET writes "(host:port)" into a
/// connection error) cannot put it into the log or the report zip. Kept in memory only, for this process.
/// </summary>
public static class AccountHosts
{
    /// <summary>What a registered name is written as.</summary>
    public const string Masked = "(account server)";

    private static readonly ConcurrentDictionary<string, byte> Names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The registered names.</summary>
    public static ICollection<string> All => Names.Keys;

    /// <summary>Registers a server name (a name without a dot, or shorter than 4 characters, is not one and is ignored).</summary>
    public static void Add(string? host)
    {
        if (host is { Length: >= 4 } && host.Contains('.', StringComparison.Ordinal))
        {
            Names.TryAdd(host.Trim().TrimEnd('.'), 0);
        }
    }

    /// <summary><paramref name="text"/> with every registered name replaced by <see cref="Masked"/>.</summary>
    public static string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text) || Names.IsEmpty)
        {
            return text ?? string.Empty;
        }

        var result = text;
        foreach (var name in Names.Keys)
        {
            result = result.Replace(name, Masked, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
