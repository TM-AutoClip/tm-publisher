namespace TMPublisher.Core.Connections;

/// <summary>
/// A value the user enters when connecting an account. <paramref name="LabelKey"/> and <paramref name="HintKey"/> are localization keys;
/// a <paramref name="Secret"/> field is masked; a <paramref name="DefaultValue"/> is pre-filled; a <paramref name="ReadOnly"/> field only
/// shows a value to copy (for example the fixed redirect address to register in the developer app).
/// </summary>
public sealed record ConnectionField(string Key, string LabelKey, bool Secret = false, bool Required = true, string? DefaultValue = null,
    string? HintKey = null, bool ReadOnly = false);
