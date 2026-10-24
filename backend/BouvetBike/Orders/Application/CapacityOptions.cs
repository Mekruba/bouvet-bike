using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Orders.Application;

/// <summary>Bound from the "Capacity" section of configuration.</summary>
public sealed class CapacityOptions
{
    public const string Section = "Capacity";

    /// <summary>
    /// The starting default for every weekday, used when the schedule is first created. After that, admins
    /// change the schedule in the staff area.
    /// </summary>
    [Range(0, 1000)]
    public int MaxOrdersPerDay { get; init; } = 5;

    /// <summary>How far ahead to look for the next open date before giving up.</summary>
    [Range(1, 365)]
    public int SearchDays { get; init; } = 60;
}
