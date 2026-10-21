using System.Net.Mail;

namespace BouvetBike.Shared.Domain;

/// <summary>A syntactically valid email address, normalized to lower case.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainException("Email is required.");
        }

        if (normalized.Length > MaxLength
            || !MailAddress.TryCreate(normalized, out var parsed)
            || parsed.Address != normalized)
        {
            throw new DomainException($"'{value}' is not a valid email address.");
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
