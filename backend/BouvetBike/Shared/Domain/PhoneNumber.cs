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

    private PhoneNumber(string value) => Value = value;

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

    public override string ToString() => Value;
}
