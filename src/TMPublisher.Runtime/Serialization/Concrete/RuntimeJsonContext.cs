using System.Text.Json.Serialization;
using TMPublisher.Runtime.Ipc;
using TMPublisher.Runtime.Notifications;

namespace TMPublisher.Runtime;

/// <summary>Source-generated JSON of the shared runtime (language files, messages between the two processes). No reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(NotificationMessage))]
[JsonSerializable(typeof(SchedulerState))]
internal sealed partial class RuntimeJsonContext : JsonSerializerContext;
