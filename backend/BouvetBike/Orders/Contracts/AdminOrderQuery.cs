using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

/// <summary>Columns the admin order list can be sorted by. Ties are broken by creation time, so pages are stable.</summary>
public enum OrderSort
{
    /// <summary>The default: what's due first.</summary>
    DueDate,

    /// <summary>When the order was placed.</summary>
    CreatedAt,

    /// <summary>Alphabetically, ignoring case.</summary>
    CustomerName,

    /// <summary>By status name.</summary>
    Status,
}

/// <summary>Admin search and filter, bound from the query string.</summary>
/// <param name="Search">Matches reference, customer name, email, phone or bike brand.</param>
public sealed record AdminOrderQuery(
    [property: StringLength(100)] string? Search = null,
    OrderStatus? Status = null,
    Guid? ServiceTypeId = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    OrderSort Sort = OrderSort.DueDate,
    bool Descending = false,
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);
