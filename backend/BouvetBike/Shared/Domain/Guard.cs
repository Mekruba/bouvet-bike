namespace BouvetBike.Shared.Domain;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.");
        }

        return MaxLength(trimmed, field, maxLength);
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : MaxLength(trimmed, field, maxLength);
    }

    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new DomainException($"{field} is required.") : value;

    private static string MaxLength(string value, string field, int maxLength) =>
        value.Length > maxLength
            ? throw new DomainException($"{field} must be at most {maxLength} characters.")
            : value;
}
