using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>A date whose capacity differs from its weekday default: extra slots, fewer, or 0 to close it.</summary>
public sealed class CapacityOverride : AggregateRoot
{
    public const int ReasonMaxLength = 200;

    public DateOnly Date { get; private set; }
    public int MaxOrders { get; private set; }
    public string? Reason { get; private set; }

    private CapacityOverride() { } // EF Core

    /// <exception cref="DomainException">The date is in the past, or the capacity is out of range.</exception>
    public static CapacityOverride Create(DateOnly date, int maxOrders, string? reason, TimeProvider clock)
    {
        var capacity = new CapacityOverride { Date = date };
        capacity.Change(maxOrders, reason, clock);
        return capacity;
    }

    public void Change(int maxOrders, string? reason, TimeProvider clock)
    {
        if (Date < DateOnly.FromDateTime(clock.GetLocalNow().Date))
        {
            throw new DomainException("The capacity of a past date can't be changed.");
        }

        if (maxOrders is < 0 or > CapacitySchedule.MaxPerDay)
        {
            throw new DomainException($"Capacity must be between 0 (closed) and {CapacitySchedule.MaxPerDay}.");
        }

        MaxOrders = maxOrders;
        Reason = Guard.Optional(reason, "Reason", ReasonMaxLength);
    }
}
