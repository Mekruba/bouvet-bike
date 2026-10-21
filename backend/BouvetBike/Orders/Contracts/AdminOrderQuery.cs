using System.ComponentModel.DataAnnotations;
using BouvetBike.Orders.Domain;

namespace BouvetBike.Orders.Contracts;

public enum OrderSort
{
    DueDate,
    CreatedAt,
    CustomerName,
    Status,
}

/// <summary>Admin search and filter, bound from the query string.</summary>
/// <param name="Search">Matches reference, customer name, email, phone or bike brand.</param>
public sealed record AdminOrderQuery(
    [StringLength(100)] string? Search = null,
    OrderStatus? Status = null,
    Guid? ServiceTypeId = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    OrderSort Sort = OrderSort.DueDate,
    bool Descending = false,
    [Range(1, int.MaxValue)] int Page = 1,
    [Range(1, 100)] int PageSize = 20);
