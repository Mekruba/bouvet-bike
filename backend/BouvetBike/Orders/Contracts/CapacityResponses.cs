namespace BouvetBike.Orders.Contracts;

/// <summary>Bookings for one due date. Used by the date picker and the staff capacity overview.</summary>
/// <param name="Capacity">This date's capacity: its override if it has one, otherwise the weekday default.</param>
/// <param name="DefaultCapacity">The weekday default, so a deviation can be shown (e.g. "6/5").</param>
public sealed record DayCapacityResponse(DateOnly Date, int Booked, int Capacity, int DefaultCapacity)
{
    /// <summary>Slots left; 0 rather than negative when the date is overbooked (capacity lowered after booking).</summary>
    public int Remaining => Math.Max(Capacity - Booked, 0);

    /// <summary>No more orders can be placed on the date.</summary>
    public bool IsFull => Booked >= Capacity;

    /// <summary>The shop is closed on the date (capacity 0).</summary>
    public bool IsClosed => Capacity == 0;

    /// <summary>The date has an override that differs from its weekday default.</summary>
    public bool IsAdjusted => Capacity != DefaultCapacity;
}
