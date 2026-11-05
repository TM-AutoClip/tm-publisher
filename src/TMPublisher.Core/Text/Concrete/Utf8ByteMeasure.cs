using System.Text;

namespace TMPublisher.Core.Text;

/// <summary>Counts UTF-8 bytes (YouTube limits its description to 5,000 bytes; a Turkish letter is 2 bytes).</summary>
public sealed class Utf8ByteMeasure : ITextMeasure
{
    /// <summary>Shared instance.</summary>
    public static Utf8ByteMeasure Instance { get; } = new();

    /// <inheritdoc />
    public int Measure(string? text) => string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
}
