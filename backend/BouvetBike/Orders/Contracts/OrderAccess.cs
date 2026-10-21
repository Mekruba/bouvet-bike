namespace BouvetBike.Orders.Contracts;

/// <summary>How guests find their orders without an account.</summary>
public static class OrderAccess
{
    /// <summary>
    /// Request header with an email or phone number, on <c>GET /api/orders</c>: lists the orders with that contact.
    /// A header rather than a query parameter keeps personal data out of URLs, browser history and server logs.
    /// </summary>
    public const string ContactHeader = "Order-Contact";
}
