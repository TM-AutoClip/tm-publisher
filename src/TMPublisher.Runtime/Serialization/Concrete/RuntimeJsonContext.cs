using System.Text.Json.Serialization;
using TMPublisher.Runtime.Notifications;

namespace TMPublisher.Runtime;

/// <summary>Source-generated JSON of the shared runtime (language files, notification lines). No reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(NotificationMessage))]
internal sealed partial class RuntimeJsonContext : JsonSerializerContext;
