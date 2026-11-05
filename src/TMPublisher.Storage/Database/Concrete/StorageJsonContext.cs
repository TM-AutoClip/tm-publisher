using System.Text.Json.Serialization;
using TMPublisher.Core.Posts;
using TMPublisher.Core.Settings;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Storage;

/// <summary>Source-generated JSON for the storage's own documents (no reflection; works with trimming).</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(KeyEnvelope))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(List<MediaItem>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;
