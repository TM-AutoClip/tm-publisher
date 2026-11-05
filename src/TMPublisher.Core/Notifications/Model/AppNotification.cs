namespace TMPublisher.Core.Notifications;

/// <summary>A notification already in the user's language.</summary>
public sealed record AppNotification(NotificationKind Kind, string Title, string Message);
