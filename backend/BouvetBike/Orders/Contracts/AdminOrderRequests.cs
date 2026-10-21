using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Staff edit of an order's details. The service can be changed here too.</summary>
public sealed record UpdateOrderRequest(
    [Required, StringLength(ContactInfo.NameMaxLength)] string Name,
    [Required, EmailAddress, StringLength(Email.MaxLength)] string Email,
    [Required, StringLength(ContactInputLimits.Phone)] string Phone,
    [Required, StringLength(MaintenanceOrder.BikeBrandMaxLength)] string BikeBrand,
    [StringLength(MaintenanceOrder.BikeModelMaxLength)] string? BikeModel,
    [Required] Guid? ServiceTypeId,
    [Required] DateOnly? DueDate);

public sealed record ChangeStatusRequest(
    [Required] OrderStatus? Status,
    [StringLength(OrderStatusChange.ReasonMaxLength)] string? Reason);

/// <param name="Notes">Replaces the internal notes; empty clears them.</param>
public sealed record UpdateInternalNotesRequest(
    [StringLength(MaintenanceOrder.NotesMaxLength)] string? Notes);

public sealed record ApplyDiscountRequest(
    [Required, StringLength(DiscountCode.CodeMaxLength)] string Code);

/// <param name="Amount">Final price in NOK.</param>
public sealed record OverridePriceRequest(
    [Required, Range(0, 1_000_000)] decimal? Amount,
    [Required, StringLength(PriceOverride.ReasonMaxLength)] string Reason);
