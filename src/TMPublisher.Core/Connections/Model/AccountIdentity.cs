namespace TMPublisher.Core.Connections;

/// <summary>Who the connected credentials belong to, as read from the platform.</summary>
public sealed record AccountIdentity(string ExternalId, string DisplayName, string? Handle);
