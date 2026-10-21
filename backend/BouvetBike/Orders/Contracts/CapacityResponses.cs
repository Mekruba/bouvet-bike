namespace BouvetBike.Orders.Contracts;

/// <summary>Bookings for one due date. Used by the date picker and the admin capacity overview.</summary>
public sealed record DayCapacityResponse(DateOnly Date, int Booked, int Capacity)
{
    public int Remaining => Math.Max(Capacity - Booked, 0);
    public bool IsFull => Booked >= Capacity;
}
