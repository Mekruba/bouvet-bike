using BouvetBike.Discounts.Domain;

namespace BouvetBike.Discounts.Contracts;

/// <param name="Value">Percent for <see cref="DiscountType.Percentage"/>, NOK for <see cref="DiscountType.FixedAmount"/>.</param>
public sealed record DiscountCodeResponse(
    Guid Id,
    string Code,
    DiscountType Type,
    decimal Value,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    int? MaxUses,
    int TimesUsed,
    bool IsActive);
