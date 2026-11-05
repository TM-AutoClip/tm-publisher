using TMPublisher.Core.Localization;

namespace TMPublisher.Tests.Scheduling;

/// <summary>Returns the key (and the arguments) instead of a translation.</summary>
internal sealed class KeyLocalizer : ILocalizer
{
    public string Language => "en";

    public string this[string key] => key;

    public string Format(string key, params object[] args) => key + "(" + string.Join(",", args) + ")";
}
