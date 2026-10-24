using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>One step in an order's status history.</summary>
public sealed class OrderStatusChange : Entity
{
    public const int ReasonMaxLength = 500;

    public OrderStatus From { get; private set; }
    public OrderStatus To { get; private set; }
    public Guid? ChangedById { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? Reason { get; private set; }

    private OrderStatusChange() { } // EF Core

    /// <summary>Created only by <see cref="MaintenanceOrder.ChangeStatus"/>, after the transition is checked.</summary>
    /// <exception cref="DomainException">The reason is longer than <see cref="ReasonMaxLength"/>.</exception>
    internal OrderStatusChange(OrderStatus from, OrderStatus to, Guid? changedById, DateTime changedAt, string? reason)
    {
        From = from;
        To = to;
        ChangedById = changedById;
        ChangedAt = changedAt;
        Reason = Guard.Optional(reason, "Reason", ReasonMaxLength);
    }
}
