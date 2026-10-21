using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>A customer (guest or logged in) placing an order.</summary>
public sealed record PlaceOrderRequest(
    [Required, StringLength(ContactInfo.NameMaxLength)] string Name,
    [Required, EmailAddress, StringLength(Email.MaxLength)] string Email,
    [Required, StringLength(ContactInputLimits.Phone)] string Phone,
    [Required, StringLength(MaintenanceOrder.BikeBrandMaxLength)] string BikeBrand,
    [StringLength(MaintenanceOrder.BikeModelMaxLength)] string? BikeModel,
    [Required] Guid? ServiceTypeId,
    [Required] DateOnly? DueDate,
    [StringLength(MaintenanceOrder.NotesMaxLength)] string? Notes,
    [StringLength(DiscountCode.CodeMaxLength)] string? DiscountCode);
