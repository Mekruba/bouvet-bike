
namespace BouvetBike.Discounts.Domain;

/// <summary>How a discount code's value is applied.</summary>
public enum DiscountType
{
    /// <summary>The value is a percentage of the price.</summary>
    Percentage,

    /// <summary>The value is an amount in NOK, capped at the price.</summary>
    FixedAmount,
}
