using System.Net.Mail;

namespace BouvetBike.Shared.Domain;

/// <summary>A syntactically valid email address, normalized to lower case.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    public string Value { get; }

    /// <summary>Only through <see cref="Create"/>, which validates.</summary>
    private Email(string value) => Value = value;

    /// <summary>Trims, lower-cases and validates an address, so the same address always compares equal.</summary>
    /// <exception cref="DomainException">Missing, longer than <see cref="MaxLength"/>, or not a valid address.</exception>
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

    /// <summary>The normalized address.</summary>
    public override string ToString() => Value;
}
