namespace BouvetBike.Orders.Contracts;

/// <summary>
/// Daily capacity: at most a configured number of non-cancelled orders per due date (5 by default).
/// Lives outside the order aggregate because it spans many orders.
/// </summary>
public interface ICapacityService
{
    /// <summary>Bookings per date, starting at <see cref="CapacityQuery.From"/> (default today).</summary>
    Task<IReadOnlyList<DayCapacityResponse>> GetAsync(CapacityQuery query, CancellationToken ct);

    /// <param name="ignoreOrderId">An order being moved to a new date, so it doesn't count against itself.</param>
    /// <exception cref="Domain.DueDateFullException">The date is full; carries the next available date.</exception>
    Task EnsureAvailableAsync(DateOnly date, Guid? ignoreOrderId, CancellationToken ct);
}
