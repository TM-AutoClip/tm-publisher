using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Data.Sqlite;

namespace TMPublisher.Storage.Database;

/// <summary>
/// How values are stored (the formats EF Core used in 0.1.0–0.1.2): a GUID as upper-case "D" text, a time as UTC ticks, a number for a state,
/// 0/1 for a flag, a list or map as source-generated JSON (no reflection, safe for trimming and native compilation).
/// </summary>
internal static class Columns
{
    /// <summary>A GUID as stored.</summary>
    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();

    /// <summary>An optional GUID as stored.</summary>
    public static object OptionalId(Guid? id) => id is { } value ? Id(value) : DBNull.Value;

    /// <summary>An optional time as stored.</summary>
    public static object OptionalTicks(DateTimeOffset? value) => value is { } time ? time.UtcTicks : DBNull.Value;

    /// <summary>An optional text as stored.</summary>
    public static object OptionalText(string? value) => value is null ? DBNull.Value : value;

    /// <summary>A JSON column.</summary>
    public static string Json<T>(T value, JsonTypeInfo<T> info) => JsonSerializer.Serialize(value, info);

    /// <summary>Reads a GUID column.</summary>
    public static Guid ReadId(SqliteDataReader reader, int ordinal) => Guid.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);

    /// <summary>Reads an optional GUID column.</summary>
    public static Guid? ReadOptionalId(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : ReadId(reader, ordinal);

    /// <summary>Reads a time column.</summary>
    public static DateTimeOffset ReadTime(SqliteDataReader reader, int ordinal) => new(reader.GetInt64(ordinal), TimeSpan.Zero);

    /// <summary>Reads an optional time column.</summary>
    public static DateTimeOffset? ReadOptionalTime(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : ReadTime(reader, ordinal);

    /// <summary>Reads an optional text column.</summary>
    public static string? ReadOptionalText(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    /// <summary>Reads a JSON column (an empty value gives a new <typeparamref name="T"/>).</summary>
    public static T ReadJson<T>(SqliteDataReader reader, int ordinal, JsonTypeInfo<T> info)
        where T : new()
    {
        var text = reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        return string.IsNullOrEmpty(text) ? new T() : JsonSerializer.Deserialize(text, info) ?? new T();
    }

    /// <summary>A command on the connection (and transaction) with named parameters (<c>$name</c>).</summary>
    public static SqliteCommand Command(SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }
}
