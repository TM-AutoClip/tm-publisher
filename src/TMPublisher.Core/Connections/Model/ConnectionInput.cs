namespace TMPublisher.Core.Connections;

/// <summary>Values the user entered in the connection window.</summary>
/// <param name="Values">By field key.</param>
public sealed record ConnectionInput(IReadOnlyDictionary<string, string> Values)
{
    /// <summary>The trimmed value or an empty string.</summary>
    public string this[string key] => Values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    /// <summary>Never prints the values (they may be secrets).</summary>
    public override string ToString() => $"ConnectionInput({Values.Count} values)";
}
