namespace BouvetBike.Orders.Contracts;

/// <summary>Bookings for one due date. Used by the date picker and the staff capacity overview.</summary>
/// <param name="Capacity">This date's capacity: its override if it has one, otherwise the weekday default.</param>
/// <param name="DefaultCapacity">The weekday default, so a deviation can be shown (e.g. "6/5").</param>
public sealed record DayCapacityResponse(DateOnly Date, int Booked, int Capacity, int DefaultCapacity)
{
    public int Remaining => Math.Max(Capacity - Booked, 0);
    public bool IsFull => Booked >= Capacity;
    public bool IsClosed => Capacity == 0;
    public bool IsAdjusted => Capacity != DefaultCapacity;
}
