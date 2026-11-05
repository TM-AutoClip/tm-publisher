using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TMPublisher.Storage.Database;

/// <summary>JSON text column for small collections (tags, media list, options), with a comparer so changes inside the list are saved.</summary>
internal static class JsonColumn
{
    /// <summary>Converter for <typeparamref name="T"/>.</summary>
    public static ValueConverter<T, string> Converter<T>(JsonTypeInfo<T> info)
        where T : new() =>
        new(v => JsonSerializer.Serialize(v, info), v => string.IsNullOrEmpty(v) ? new T() : JsonSerializer.Deserialize(v, info) ?? new T());

    /// <summary>Comparer that compares the JSON form.</summary>
    public static ValueComparer<T> Comparer<T>(JsonTypeInfo<T> info)
        where T : new() =>
        new((a, b) => JsonSerializer.Serialize(a!, info) == JsonSerializer.Serialize(b!, info),
            v => JsonSerializer.Serialize(v, info).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize(JsonSerializer.Serialize(v, info), info) ?? new T());
}
