using BouvetBike.Shared.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BouvetBike.Shared.Persistence;

// Conversions registered as conventions in AppDbContext. Single-value value objects are stored as plain strings.

public class EmailConverter() : ValueConverter<Email, string>(v => v.Value, v => Email.Create(v));

public class PhoneNumberConverter() : ValueConverter<PhoneNumber, string>(v => v.Value, v => PhoneNumber.Create(v));

/// <summary>
/// All timestamps are UTC. SQLite doesn't store <see cref="DateTime.Kind"/>, so without this a value read back
/// is Unspecified and is serialized without a "Z", which clients would read as local time.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
