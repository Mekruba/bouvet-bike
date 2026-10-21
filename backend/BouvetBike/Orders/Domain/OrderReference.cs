using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>
/// Customer-facing order reference, e.g. <c>BB-7K3QX9MD</c>. Random rather than sequential because
/// it is half of the guest lookup credential (reference + email/phone). The alphabet leaves out
/// look-alike characters (0/O, 1/I/L) so it is easy to read out over the phone.
/// </summary>
public sealed record OrderReference
{
    public const string Prefix = "BB-";
    public const int CodeLength = 8;
    public const int MaxLength = 11; // Prefix.Length + CodeLength
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public string Value { get; }

    private OrderReference(string value) => Value = value;

    public static OrderReference New() => new(Prefix + RandomNumberGenerator.GetString(Alphabet, CodeLength));

    /// <summary>Accepts the code with or without prefix, in any case.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out OrderReference? reference)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (code.StartsWith(Prefix))
        {
            code = code[Prefix.Length..];
        }

        reference = code.Length == CodeLength && code.All(Alphabet.Contains) ? new OrderReference(Prefix + code) : null;
        return reference is not null;
    }

    public static OrderReference Parse(string? value) =>
        TryParse(value, out var reference) ? reference : throw new DomainException($"'{value}' is not a valid order reference.");

    public override string ToString() => Value;
}
