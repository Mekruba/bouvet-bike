using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <param name="EmailOrPhone">Proves access for guests. Not needed when the logged-in customer owns the order.</param>
/// <param name="Notes">Replaces the notes; empty clears them.</param>
public sealed record UpdateCustomerNotesRequest(
    [StringLength(ContactInputLimits.EmailOrPhone)] string? EmailOrPhone,
    [StringLength(MaintenanceOrder.NotesMaxLength)] string? Notes);
