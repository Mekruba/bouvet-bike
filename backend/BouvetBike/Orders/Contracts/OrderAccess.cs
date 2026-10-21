namespace BouvetBike.Orders.Contracts;

/// <summary>How guests prove access to an order without an account.</summary>
public static class OrderAccess
{
    /// <summary>
    /// Request header carrying the email or phone number on the order. A header rather than a query
    /// parameter keeps personal data out of URLs, browser history and server logs.
    /// </summary>
    public const string ContactHeader = "Order-Contact";
}
