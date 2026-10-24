using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Orders.Contracts;

/// <summary>Which days <c>GET /api/capacity</c> lists, bound from the query string.</summary>
/// <param name="From">First day; defaults to today.</param>
/// <param name="Days">Number of days to list.</param>
public sealed record CapacityQuery(DateOnly? From = null, [property: Range(1, CapacityQuery.MaxDays)] int Days = 14)
{
    public const int MaxDays = 60;
}
