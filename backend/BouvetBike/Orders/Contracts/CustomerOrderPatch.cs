using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>What a customer may change on their order with JSON Patch: only <c>/notes</c>, and only while it's New.</summary>
public sealed class CustomerOrderPatch
{
    [StringLength(MaintenanceOrder.NotesMaxLength)]
    public string? Notes { get; set; }
}
