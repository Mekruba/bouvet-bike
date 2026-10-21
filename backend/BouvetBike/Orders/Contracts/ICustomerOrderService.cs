namespace BouvetBike.Orders.Contracts;

/// <summary>
/// Customer-facing order use cases. Works for guests (who prove access with reference + email/phone)
/// and for logged-in customers (who own the order through <c>CustomerId</c>).
/// </summary>
public interface ICustomerOrderService
{
    /// <summary>Estimated price for a service and optional discount code. Nothing is saved or used up.</summary>
    Task<OrderPriceResponse> QuoteAsync(PriceQuoteRequest request, CancellationToken ct);

    /// <summary>
    /// Checks capacity for the due date, redeems the discount code if given, and saves the order.
    /// Links the order to the current user when logged in.
    /// </summary>
    /// <exception cref="Domain.DueDateFullException">The due date is fully booked.</exception>
    Task<CustomerOrderResponse> PlaceAsync(PlaceOrderRequest request, CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">No order matches both reference and email/phone.</exception>
    Task<CustomerOrderResponse> LookupAsync(LookupOrderRequest request, CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">The caller can't access this order.</exception>
    Task<CustomerOrderResponse> UpdateNotesAsync(string reference, UpdateCustomerNotesRequest request, CancellationToken ct);

    /// <summary>The logged-in customer's orders, newest first.</summary>
    Task<IReadOnlyList<CustomerOrderSummaryResponse>> GetMyOrdersAsync(CancellationToken ct);
}
