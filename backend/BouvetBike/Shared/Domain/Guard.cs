namespace BouvetBike.Shared.Domain;

/// <summary>Input checks shared by the domain types. Each throws a <see cref="DomainException"/> naming the field.</summary>
internal static class Guard
{
    /// <summary>A trimmed, non-empty string of at most <paramref name="maxLength"/> characters.</summary>
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.");
        }

        return MaxLength(trimmed, field, maxLength);
    }

    /// <summary>A trimmed string of at most <paramref name="maxLength"/> characters, or null when empty or whitespace.</summary>
    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : MaxLength(trimmed, field, maxLength);
    }

    /// <summary>The ID, unless it is <see cref="Guid.Empty"/>.</summary>
    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new DomainException($"{field} is required.") : value;

    /// <summary>The value, unless it is longer than <paramref name="maxLength"/>.</summary>
    private static string MaxLength(string value, string field, int maxLength) =>
        value.Length > maxLength
            ? throw new DomainException($"{field} must be at most {maxLength} characters.")
            : value;
}
