namespace BouvetBike.Shared.Contracts;

/// <summary>One page of a list, with what a client needs to page through the rest.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="TotalCount">Items across all pages.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Number of pages, at least 0.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
