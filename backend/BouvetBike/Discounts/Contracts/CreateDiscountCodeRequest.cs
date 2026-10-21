using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;

namespace BouvetBike.Discounts.Contracts;

/// <param name="Code">Letters, digits and dashes; stored upper case and matched case-insensitively.</param>
/// <param name="Value">Percent (0–100] for <see cref="DiscountType.Percentage"/>, NOK for <see cref="DiscountType.FixedAmount"/>.</param>
/// <param name="MaxUses">Null for unlimited.</param>
public sealed record CreateDiscountCodeRequest(
    [Required, StringLength(DiscountCode.CodeMaxLength), RegularExpression("^[A-Za-z0-9-]+$")] string Code,
    [Required] DiscountType? Type,
    [Required, Range(0.01, 1_000_000)] decimal? Value,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null,
    [Range(1, int.MaxValue)] int? MaxUses = null);
