using BouvetBike.Discounts.Domain;

namespace BouvetBike.Discounts.Contracts;

/// <summary>Discount code checks for other modules (Orders). Codes are matched case-insensitively.</summary>
public interface IDiscountService
{
    /// <summary>Checks the code is usable now, without using it up. For price quotes.</summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown, inactive, expired or used-up code.</exception>
    Task<AppliedDiscount> PreviewAsync(string code, CancellationToken ct);

    /// <summary>
    /// Checks the code and counts one use. The change is tracked on the shared DbContext,
    /// so it's saved in the same transaction as the order that uses it.
    /// </summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown, inactive, expired or used-up code.</exception>
    Task<AppliedDiscount> RedeemAsync(string code, CancellationToken ct);
}
