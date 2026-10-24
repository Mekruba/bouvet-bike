using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Estimated price before ordering, bound from the query string. Checks the discount code without using it up.</summary>
public sealed record PriceQuoteQuery(
    [property: Required] Guid? ServiceTypeId,
    [property: StringLength(DiscountCode.CodeMaxLength)] string? DiscountCode);
