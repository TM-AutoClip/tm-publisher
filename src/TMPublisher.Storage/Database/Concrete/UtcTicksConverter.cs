using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TMPublisher.Storage.Database;

/// <summary>
/// Stores <see cref="DateTimeOffset"/> as UTC ticks (INTEGER), the format of every version so far: SQLite has no date type, and a number
/// sorts and compares correctly in queries. Read back with offset zero.
/// </summary>
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
