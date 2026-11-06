namespace TMPublisher.Desktop.Paths;

/// <summary>How the window draws: with the graphics card (<c>--gpu</c>) or the processor (default). Shown in a problem report.</summary>
public sealed record DrawingMode(bool Gpu);
