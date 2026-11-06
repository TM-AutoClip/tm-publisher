using TMPublisher.Core.Notifications;

namespace TMPublisher.Runtime.Notifications;

/// <summary>
/// A corner notification a run sends to the short-lived notification process, already in the
/// user's language, with how long it stays on screen.
/// </summary>
/// <param name="Kind">Kind.</param>
/// <param name="Title">Title.</param>
/// <param name="Message">Message.</param>
/// <param name="Seconds">How long it stays (seconds).</param>
public sealed record NotificationMessage(NotificationKind Kind, string Title, string Message, int Seconds);
