namespace TMPublisher.Core.Text;

/// <summary>How a platform counts the length of a text.</summary>
public interface ITextMeasure
{
    /// <summary>Length as the platform counts it.</summary>
    int Measure(string? text);
}
