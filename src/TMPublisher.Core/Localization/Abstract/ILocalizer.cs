namespace TMPublisher.Core.Localization;

/// <summary>User-facing text in the chosen language (English and Turkish).</summary>
public interface ILocalizer
{
    /// <summary>Current language code (<c>en</c> or <c>tr</c>).</summary>
    string Language { get; }

    /// <summary>Text of the key (the key itself when missing).</summary>
    string this[string key] { get; }

    /// <summary>Text of the key with <c>{0}</c>-style arguments, current culture.</summary>
    string Format(string key, params object[] args);
}
