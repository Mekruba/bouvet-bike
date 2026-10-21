using BouvetBike.Discounts.Domain;

namespace BouvetBike.Discounts.Persistence;

/// <summary>Demo discount codes, so the discount and price quote features can be shown. Development only.</summary>
public static class DiscountSeed
{
    public static IEnumerable<DiscountCode> DemoCodes() =>
    [
        DiscountCode.Create("WELCOME10", DiscountType.Percentage, 10),
        DiscountCode.Create("SPRING100", DiscountType.FixedAmount, 100, maxUses: 50),
    ];
}
