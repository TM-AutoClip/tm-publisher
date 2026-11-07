using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TMPublisher.Storage.Database;

/// <summary>
/// A list or map stored as JSON text with source-generated serialization (no reflection). An empty stored value reads as a new
/// <typeparamref name="T"/>.
/// </summary>
public sealed class JsonValueConverter<T>(JsonTypeInfo<T> info) : ValueConverter<T, string>(
    value => JsonSerializer.Serialize(value, info),
    text => string.IsNullOrEmpty(text) ? new T() : JsonSerializer.Deserialize(text, info) ?? new T())
    where T : new();
