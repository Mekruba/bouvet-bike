using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>
/// What staff may change on an order with JSON Patch. Status has its own resource
/// (<c>status-changes</c>) and the price override is Admin-only (<c>price-override</c>).
/// Setting <c>/discountCode</c> redeems the code; removing it removes the discount.
/// </summary>
public sealed class AdminOrderPatch
{
    [Required, StringLength(ContactInfo.NameMaxLength)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(global::BouvetBike.Shared.Domain.Email.MaxLength)]
    public string Email { get; set; } = "";

    [Required, StringLength(ContactInputLimits.Phone)]
    public string Phone { get; set; } = "";

    [Required, StringLength(MaintenanceOrder.BikeBrandMaxLength)]
    public string BikeBrand { get; set; } = "";

    [StringLength(MaintenanceOrder.BikeModelMaxLength)]
    public string? BikeModel { get; set; }

    public Guid ServiceTypeId { get; set; }

    public DateOnly DueDate { get; set; }

    [StringLength(MaintenanceOrder.NotesMaxLength)]
    public string? InternalNotes { get; set; }

    [StringLength(global::BouvetBike.Discounts.Domain.DiscountCode.CodeMaxLength)]
    public string? DiscountCode { get; set; }
}
