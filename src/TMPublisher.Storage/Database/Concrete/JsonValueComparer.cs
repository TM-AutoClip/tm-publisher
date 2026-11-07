using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TMPublisher.Storage.Database;

/// <summary>Compares a JSON column by its JSON form, so a change inside the list or map (an added tag, a removed account) is saved.</summary>
public sealed class JsonValueComparer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
    JsonTypeInfo<T> info) : ValueComparer<T>(
    (a, b) => JsonSerializer.Serialize(a!, info) == JsonSerializer.Serialize(b!, info),
    value => JsonSerializer.Serialize(value, info).GetHashCode(StringComparison.Ordinal),
    value => JsonSerializer.Deserialize(JsonSerializer.Serialize(value, info), info) ?? new T())
    where T : new();
