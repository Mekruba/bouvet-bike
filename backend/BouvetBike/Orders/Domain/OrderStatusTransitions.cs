
namespace BouvetBike.Orders.Domain;

/// <summary>The order lifecycle. Anything not listed here is an invalid transition.</summary>
public static class OrderStatusTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.New] = [OrderStatus.InProgress, OrderStatus.WaitingForCustomer, OrderStatus.Cancelled],
        [OrderStatus.InProgress] = [OrderStatus.WaitingForCustomer, OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.WaitingForCustomer] = [OrderStatus.InProgress, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
    };

    /// <summary>The statuses an order can move to from <paramref name="status"/>; the staff UI shows them as actions.</summary>
    public static IReadOnlyList<OrderStatus> From(OrderStatus status) => Allowed[status];

    /// <summary>Whether an order may move from one status to the other.</summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    /// <summary>A final status (Completed or Cancelled), with no moves out of it.</summary>
    public static bool IsTerminal(this OrderStatus status) => Allowed[status].Length == 0;
}
