using System.Text.Json.Serialization;
using TMPublisher.Background.Hosting;
using TMPublisher.Background.Triggers;

namespace TMPublisher.Background.Serialization;

/// <summary>Source-generated JSON of the small plain files in the data folder (no secrets in them). No reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(TriggerRecord))]
[JsonSerializable(typeof(RunState))]
internal sealed partial class BackgroundJsonContext : JsonSerializerContext;
