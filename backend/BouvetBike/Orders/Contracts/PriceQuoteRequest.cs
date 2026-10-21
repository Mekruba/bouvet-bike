using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Estimated price before ordering. Checks the discount code without using it up.</summary>
public sealed record PriceQuoteRequest(
    [Required] Guid? ServiceTypeId,
    [StringLength(DiscountCode.CodeMaxLength)] string? DiscountCode);
