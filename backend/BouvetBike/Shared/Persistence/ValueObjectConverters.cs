using BouvetBike.Shared.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BouvetBike.Shared.Persistence;

// Shared single-value value objects are stored as plain strings. Registered as conventions in AppDbContext.

public class EmailConverter() : ValueConverter<Email, string>(v => v.Value, v => Email.Create(v));

public class PhoneNumberConverter() : ValueConverter<PhoneNumber, string>(v => v.Value, v => PhoneNumber.Create(v));
