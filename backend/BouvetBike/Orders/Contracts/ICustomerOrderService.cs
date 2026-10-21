using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Orders.Contracts;

/// <summary>
/// Customer-facing order use cases. Works for guests, who prove access with the email or phone on the
/// order (<see cref="OrderAccess.ContactHeader"/>), and for logged-in customers, who own their orders.
/// </summary>
public interface ICustomerOrderService
{
    /// <summary>Estimated price for a service and optional discount code. Nothing is saved or used up.</summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown or inactive service, or an unusable discount code.</exception>
    Task<OrderPriceResponse> QuoteAsync(PriceQuoteQuery query, CancellationToken ct);

    /// <summary>
    /// Checks capacity for the due date, redeems the discount code if given, and saves the order, all or nothing.
    /// Links the order to the current user when logged in.
    /// </summary>
    /// <exception cref="Domain.DueDateFullException">The due date is fully booked; carries the next available date.</exception>
    /// <exception cref="Shared.Domain.DomainException">Unknown or inactive service, past due date, invalid contact, or an unusable discount code.</exception>
    Task<CustomerOrderResponse> PlaceAsync(PlaceOrderRequest request, CancellationToken ct);

    /// <param name="reference">With or without the <c>BB-</c> prefix, any case.</param>
    /// <param name="contact">Email or phone on the order; not needed when the logged-in customer owns it.</param>
    /// <exception cref="Shared.Application.NotFoundException">No such order, or the caller can't access it (indistinguishable on purpose).</exception>
    Task<CustomerOrderResponse> GetAsync(string reference, string? contact, CancellationToken ct);

    /// <summary>JSON Patch on <see cref="CustomerOrderPatch"/>: only <c>/notes</c>, only while the order is New.</summary>
    /// <exception cref="Shared.Application.NotFoundException">As for <see cref="GetAsync"/>.</exception>
    /// <exception cref="Shared.Application.UnprocessableException">Other paths, invalid notes, or the repair has started.</exception>
    Task<CustomerOrderResponse> PatchAsync(string reference, string? contact, JsonPatchDocument<CustomerOrderPatch> patch, CancellationToken ct);

    /// <summary>The logged-in customer's orders, newest first.</summary>
    Task<IReadOnlyList<CustomerOrderSummaryResponse>> GetMyOrdersAsync(CancellationToken ct);
}
