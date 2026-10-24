namespace BouvetBike.Shared.Domain;

/// <summary>
/// A phone number in E.164 format (e.g. <c>+4712345678</c>). Numbers without a country code
/// are assumed to be Norwegian and must have 8 digits.
/// </summary>
public sealed record PhoneNumber
{
    public const int MaxLength = 16; // '+' and up to 15 digits
    private const string DefaultCountryCode = "47";

    public string Value { get; }

    /// <summary>Only through <see cref="Create"/>, which validates.</summary>
    private PhoneNumber(string value) => Value = value;

    /// <summary>
    /// Parses a number written the way people type it (spaces, dashes, dots and brackets allowed) into E.164.
    /// A country code is given with <c>+</c> or <c>00</c>; without one, the number must be a Norwegian 8-digit number.
    /// </summary>
    /// <exception cref="DomainException">Missing, contains other characters, or has the wrong number of digits.</exception>
    public static PhoneNumber Create(string? value)
    {
        var raw = value?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new DomainException("Phone number is required.");
        }

        var hasPlus = raw.StartsWith('+');
        var body = hasPlus ? raw[1..] : raw;
        if (!body.All(c => char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' or '.'))
        {
            throw new DomainException($"'{value}' is not a valid phone number.");
        }

        var digits = string.Concat(body.Where(char.IsAsciiDigit));
        var hasCountryCode = hasPlus || digits.StartsWith("00");
        if (digits.StartsWith("00"))
        {
            digits = digits[2..];
        }

        var valid = hasCountryCode ? digits.Length is >= 8 and <= 15 : digits.Length == 8;
        if (!valid)
        {
            throw new DomainException(
                $"'{value}' is not a valid phone number. Use 8 digits, or include the country code (+47…).");
        }

        return new PhoneNumber("+" + (hasCountryCode ? digits : DefaultCountryCode + digits));
    }

    /// <summary>The number in E.164 format.</summary>
    public override string ToString() => Value;
}
