using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Orders.Application;

/// <summary>Bound from the "Capacity" section of configuration.</summary>
public sealed class CapacityOptions
{
    public const string Section = "Capacity";

    [Range(1, 1000)]
    public int MaxOrdersPerDay { get; init; } = 5;

    /// <summary>How far ahead to look for the next free date before giving up.</summary>
    [Range(1, 365)]
    public int SearchDays { get; init; } = 60;
}
