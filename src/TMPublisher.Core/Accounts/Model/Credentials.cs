using System.Collections.ObjectModel;

namespace TMPublisher.Core.Accounts;

/// <summary>
/// The secret values of one account (client id and secret of the user's own developer app, tokens, webhook address...). Only ever kept in
/// memory and in the encrypted database; never logged, never put into an error message.
/// </summary>
/// <param name="Values">Values by key (<see cref="CredentialKeys"/>).</param>
public sealed record Credentials(IReadOnlyDictionary<string, string> Values)
{
    /// <summary>No values.</summary>
    public static Credentials Empty { get; } = new(ReadOnlyDictionary<string, string>.Empty);

    /// <summary>The value or an empty string.</summary>
    public string this[string key] => Values.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>The value, or an exception that names the missing key (not the value).</summary>
    public string Require(string key) =>
        Values.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"The account has no '{key}' value; connect it again.");

    /// <summary>A copy with the given values set (empty value removes the key).</summary>
    public Credentials With(params (string Key, string? Value)[] changes)
    {
        var copy = new Dictionary<string, string>(Values, StringComparer.Ordinal);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value))
            {
                copy.Remove(key);
            }
            else
            {
                copy[key] = value;
            }
        }

        return new Credentials(copy);
    }

    /// <summary>Expiry of the access token (UTC), when known.</summary>
    public DateTimeOffset? AccessExpiresAtUtc =>
        long.TryParse(this[CredentialKeys.AccessExpiresAt], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
            out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>Never prints the values.</summary>
    public override string ToString() => $"Credentials({Values.Count} values)";
}
