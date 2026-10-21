using System.Text.RegularExpressions;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Discounts.Domain;

public sealed partial class DiscountCode : AggregateRoot
{
    public const int CodeMaxLength = 32;

    /// <summary>Stored upper case; matched case-insensitively by normalizing input with <see cref="Normalize"/>.</summary>
    public string Code { get; private set; } = null!;
    public DiscountType Type { get; private set; }
    /// <summary>Percent (0–100] for <see cref="DiscountType.Percentage"/>, amount in NOK for <see cref="DiscountType.FixedAmount"/>.</summary>
    public decimal Value { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public int? MaxUses { get; private set; }
    public int TimesUsed { get; private set; }
    public bool IsActive { get; private set; }

    private DiscountCode() { } // EF Core

    public static DiscountCode Create(
        string code, DiscountType type, decimal value, DateOnly? validFrom = null, DateOnly? validTo = null, int? maxUses = null)
    {
        var normalized = Normalize(code);
        if (!CodeFormat().IsMatch(normalized))
        {
            throw new DomainException("Discount code may only contain letters, digits and dashes.");
        }

        var discount = new DiscountCode { Code = normalized, IsActive = true };
        discount.UpdateTerms(type, value, validFrom, validTo, maxUses);
        return discount;
    }

    public static string Normalize(string? code) => Guard.Required(code, "Discount code", CodeMaxLength).ToUpperInvariant();

    public void UpdateTerms(DiscountType type, decimal value, DateOnly? validFrom, DateOnly? validTo, int? maxUses)
    {
        var validValue = type switch
        {
            DiscountType.Percentage => value is > 0 and <= 100,
            DiscountType.FixedAmount => value > 0,
            _ => false,
        };
        if (!validValue)
        {
            throw new DomainException(type == DiscountType.Percentage
                ? "Percentage discount must be between 0 and 100."
                : "Fixed discount must be positive.");
        }

        if (validFrom > validTo)
        {
            throw new DomainException("Discount code validity must start before it ends.");
        }

        if (maxUses < 1)
        {
            throw new DomainException("Max uses must be at least 1.");
        }

        if (maxUses < TimesUsed)
        {
            throw new DomainException($"Max uses can't be lower than the {TimesUsed} times the code has already been used.");
        }

        Type = type;
        Value = value;
        ValidFrom = validFrom;
        ValidTo = validTo;
        MaxUses = maxUses;
    }

    /// <summary>Checks the code can be used today, without counting a use. For price quotes.</summary>
    public AppliedDiscount Preview(TimeProvider clock)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().Date);
        if (!IsActive || today < ValidFrom || today > ValidTo)
        {
            throw new DomainException($"Discount code '{Code}' is not valid.");
        }

        if (TimesUsed >= MaxUses)
        {
            throw new DomainException($"Discount code '{Code}' has been used up.");
        }

        return new AppliedDiscount(this);
    }

    /// <summary>Checks the code can be used today, counts the use, and returns the terms to store on the order.</summary>
    public AppliedDiscount Redeem(TimeProvider clock)
    {
        var applied = Preview(clock);
        TimesUsed++;
        return applied;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex CodeFormat();
}
