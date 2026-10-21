
namespace BouvetBike.Orders.Domain;

public enum OrderStatus
{
    New,
    InProgress,
    WaitingForCustomer,
    Completed,
    Cancelled,
}
