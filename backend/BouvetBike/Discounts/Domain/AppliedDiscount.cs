using System.Diagnostics;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Discounts.Domain;

/// <summary>
/// Snapshot of a discount code's terms at the time it was applied to an order. Later changes to
/// the code don't affect the order, and the discount is recalculated if the order's services change.
/// </summary>
public sealed record AppliedDiscount
{
    public Guid DiscountCodeId { get; private init; }
    public string Code { get; private init; } = null!;
    public DiscountType Type { get; private init; }
    public decimal Value { get; private init; }

    private AppliedDiscount() { } // EF Core

    /// <summary>Copies the code's current terms. Created only by <see cref="DiscountCode.Preview"/>, after its checks pass.</summary>
    internal AppliedDiscount(DiscountCode code)
    {
        DiscountCodeId = code.Id;
        Code = code.Code;
        Type = code.Type;
        Value = code.Value;
    }

    /// <summary>The discount on <paramref name="subtotal"/>: a percentage of it, or the fixed amount. Never more than the subtotal, so the total can't go negative.</summary>
    public Money CalculateFor(Money subtotal) => Type switch
    {
        DiscountType.Percentage => new Money(subtotal.Amount * Value / 100m, subtotal.Currency),
        DiscountType.FixedAmount => new Money(Math.Min(Value, subtotal.Amount), subtotal.Currency),
        _ => throw new UnreachableException(),
    };
}
