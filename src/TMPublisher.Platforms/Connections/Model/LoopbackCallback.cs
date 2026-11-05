namespace TMPublisher.Platforms.Connections;

/// <summary>What the browser brought back to the loopback address.</summary>
public sealed record LoopbackCallback(string? Code, string? State, string? Error);
