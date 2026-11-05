namespace TMPublisher.Core.Platforms;

/// <summary>Step-by-step guide for creating the user's own developer app and connecting it; warnings are shown above the steps.</summary>
public sealed record SetupGuide(IReadOnlyList<GuideStep> Steps, IReadOnlyList<string> WarningKeys);
