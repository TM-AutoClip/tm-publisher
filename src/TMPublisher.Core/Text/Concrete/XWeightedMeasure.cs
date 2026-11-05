namespace TMPublisher.Core.Text;

/// <summary>X's weighted length (<see cref="XText.WeightedLength"/>).</summary>
public sealed class XWeightedMeasure : ITextMeasure
{
    /// <summary>Shared instance.</summary>
    public static XWeightedMeasure Instance { get; } = new();

    /// <inheritdoc />
    public int Measure(string? text) => XText.WeightedLength(text);
}
