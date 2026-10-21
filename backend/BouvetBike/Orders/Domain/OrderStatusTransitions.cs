
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

    public static IReadOnlyList<OrderStatus> From(OrderStatus status) => Allowed[status];

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static bool IsTerminal(this OrderStatus status) => Allowed[status].Length == 0;
}
