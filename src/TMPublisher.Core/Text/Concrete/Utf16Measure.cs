namespace TMPublisher.Core.Text;

/// <summary>Counts UTF-16 code units (an emoji may count 2); the safe side for platforms that do not document their counting.</summary>
public sealed class Utf16Measure : ITextMeasure
{
    /// <summary>Shared instance.</summary>
    public static Utf16Measure Instance { get; } = new();

    /// <inheritdoc />
    public int Measure(string? text) => text?.Length ?? 0;
}
