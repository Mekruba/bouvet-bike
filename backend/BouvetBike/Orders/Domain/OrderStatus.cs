
namespace BouvetBike.Orders.Domain;

/// <summary>Where an order is in the workshop. The allowed moves are in <see cref="OrderStatusTransitions"/>.</summary>
public enum OrderStatus
{
    /// <summary>Booked, not started. The only status in which customers can change their notes.</summary>
    New,

    /// <summary>A mechanic is working on the bike.</summary>
    InProgress,

    /// <summary>Paused until the customer responds.</summary>
    WaitingForCustomer,

    /// <summary>Done. Final: no more status changes or detail edits.</summary>
    Completed,

    /// <summary>Called off. Final, and no longer counts against the day's capacity.</summary>
    Cancelled,
}
