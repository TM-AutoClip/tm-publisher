using System.Text.Json;

namespace TMPublisher.Runtime.Notifications;

/// <summary>
/// The input of the short-lived notification process (<c>--notify</c>): one JSON line per notification, written to its standard input by the
/// run that sent the posts (never on the command line).
/// </summary>
public static class NotificationLines
{
    /// <summary>The JSON line of a notification.</summary>
    public static string Write(NotificationMessage message) => JsonSerializer.Serialize(message, RuntimeJsonContext.Default.NotificationMessage);

    /// <summary>The notification of an input line; <c>null</c> when it is not one.</summary>
    public static NotificationMessage? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.StartsWith('{') ? JsonSerializer.Deserialize(line, RuntimeJsonContext.Default.NotificationMessage) : null;
    }
}
