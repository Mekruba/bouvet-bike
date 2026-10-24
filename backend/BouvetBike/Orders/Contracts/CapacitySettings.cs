using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Default capacity per weekday. 0 means closed.</summary>
public sealed record CapacityScheduleResponse(int Monday, int Tuesday, int Wednesday, int Thursday, int Friday, int Saturday, int Sunday);

/// <summary>The JSON Patch target for the schedule: paths <c>/monday</c> … <c>/sunday</c>.</summary>
public sealed class CapacitySchedulePatch
{
    [Range(0, CapacitySchedule.MaxPerDay)] public int Monday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Tuesday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Wednesday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Thursday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Friday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Saturday { get; set; }
    [Range(0, CapacitySchedule.MaxPerDay)] public int Sunday { get; set; }
}

/// <param name="MaxOrders">Capacity for the date; 0 closes it.</param>
public sealed record SetCapacityOverrideRequest(
    [Required, Range(0, CapacitySchedule.MaxPerDay)] int? MaxOrders,
    [StringLength(CapacityOverride.ReasonMaxLength)] string? Reason);

/// <param name="DefaultCapacity">The weekday default it replaces, to show e.g. "6/5".</param>
public sealed record CapacityOverrideResponse(DateOnly Date, int MaxOrders, int DefaultCapacity, string? Reason);

/// <param name="From">First date; defaults to today.</param>
/// <param name="To">Last date; defaults to 60 days after From.</param>
public sealed record CapacityOverrideQuery(DateOnly? From = null, DateOnly? To = null);
