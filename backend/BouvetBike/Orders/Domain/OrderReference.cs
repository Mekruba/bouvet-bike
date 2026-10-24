using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>
/// Customer-facing order reference, e.g. <c>BB-7K3QX9MD</c>. Random rather than sequential because
/// knowing it is enough to open the order, so it mustn't be guessable from another one. The alphabet leaves out
/// look-alike characters (0/O, 1/I/L) so it is easy to read out over the phone.
/// </summary>
public sealed record OrderReference
{
    public const string Prefix = "BB-";
    public const int CodeLength = 8;
    public const int MaxLength = 11; // Prefix.Length + CodeLength
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public string Value { get; }

    /// <summary>Only through <see cref="New"/> or <see cref="TryParse"/>, which validate.</summary>
    private OrderReference(string value) => Value = value;

    /// <summary>A new random reference from a cryptographic random source (31^8, about 850 billion, possible codes).</summary>
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

    /// <summary>Like <see cref="TryParse"/>, but throws on invalid input.</summary>
    /// <exception cref="DomainException">Not a valid reference.</exception>
    public static OrderReference Parse(string? value) =>
        TryParse(value, out var reference) ? reference : throw new DomainException($"'{value}' is not a valid order reference.");

    /// <summary>The reference with its prefix, e.g. <c>BB-7K3QX9MD</c>.</summary>
    public override string ToString() => Value;
}
