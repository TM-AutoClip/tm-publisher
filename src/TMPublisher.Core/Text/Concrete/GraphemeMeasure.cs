using System.Globalization;

namespace TMPublisher.Core.Text;

/// <summary>Counts user-perceived characters (an emoji with modifiers is one); Bluesky counts this way.</summary>
public sealed class GraphemeMeasure : ITextMeasure
{
    /// <summary>Shared instance.</summary>
    public static GraphemeMeasure Instance { get; } = new();

    /// <inheritdoc />
    public int Measure(string? text) => string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements;
}
