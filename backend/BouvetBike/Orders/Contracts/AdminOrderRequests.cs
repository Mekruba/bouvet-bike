using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Creates a status change: <c>POST /api/admin/orders/{id}/status-changes</c>.</summary>
public sealed record ChangeStatusRequest(
    [Required] OrderStatus? Status,
    [StringLength(OrderStatusChange.ReasonMaxLength)] string? Reason);

/// <summary>Sets the final price: <c>PUT /api/admin/orders/{id}/price-override</c>.</summary>
/// <param name="Amount">Final price in NOK.</param>
public sealed record OverridePriceRequest(
    [Required, Range(0, 1_000_000)] decimal? Amount,
    [Required, StringLength(PriceOverride.ReasonMaxLength)] string Reason);
