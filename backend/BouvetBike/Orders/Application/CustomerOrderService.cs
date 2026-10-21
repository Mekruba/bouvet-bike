using BouvetBike.Catalog.Contracts;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Orders.Application;

public sealed class CustomerOrderService(
    AppDbContext db,
    ICatalogService catalog,
    IDiscountService discounts,
    ICapacityService capacity,
    ICurrentUser currentUser,
    TimeProvider clock) : ICustomerOrderService
{
    public async Task<OrderPriceResponse> QuoteAsync(PriceQuoteQuery query, CancellationToken ct)
    {
        var service = await catalog.GetOrderableServiceAsync(query.ServiceTypeId!.Value, ct);
        var discount = query.DiscountCode is { Length: > 0 } code ? await discounts.PreviewAsync(code, ct) : null;

        var discountAmount = discount?.CalculateFor(service.BasePrice) ?? Money.Zero();
        return new OrderPriceResponse(
            MoneyResponse.From(service.BasePrice),
            discount?.Code,
            MoneyResponse.From(discountAmount),
            MoneyResponse.From(service.BasePrice - discountAmount),
            IsOverridden: false);
    }

    public async Task<CustomerOrderResponse> PlaceAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        var contact = new ContactInfo(request.Name, Email.Create(request.Email), PhoneNumber.Create(request.Phone));
        var service = await catalog.GetOrderableServiceAsync(request.ServiceTypeId!.Value, ct);

        // The domain checks the service and the date; capacity spans orders, so it's checked here.
        var order = MaintenanceOrder.Place(
            contact, request.BikeBrand, request.BikeModel, service, request.DueDate!.Value, clock, currentUser.Id, request.Notes);
        await capacity.EnsureAvailableAsync(order.DueDate, ignoreOrderId: null, ct);

        if (request.DiscountCode is { Length: > 0 } code)
        {
            order.ApplyDiscount(await discounts.RedeemAsync(code, ct), clock);
        }

        // One SaveChanges: the order and the discount use are saved together, or not at all.
        db.MaintenanceOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return ToResponse(order);
    }

    public async Task<CustomerOrderResponse> GetAsync(string reference, string? contact, CancellationToken ct) =>
        ToResponse(await FindAccessibleAsync(reference, contact, ct));

    public async Task<CustomerOrderResponse> PatchAsync(
        string reference, string? contact, JsonPatchDocument<CustomerOrderPatch> patch, CancellationToken ct)
    {
        var order = await FindAccessibleAsync(reference, contact, ct);
        var model = new CustomerOrderPatch { Notes = order.CustomerNotes };

        patch.ApplyAndValidate(model);
        try
        {
            order.UpdateCustomerNotes(model.Notes, clock);
        }
        catch (DomainException ex)
        {
            // RFC 5789: a patch that can't be applied in the resource's current state is 422, not 400.
            throw UnprocessableException.From(ex);
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(order);
    }

    public async Task<IReadOnlyList<CustomerOrderSummaryResponse>> GetMyOrdersAsync(CancellationToken ct)
    {
        var customerId = currentUser.Id ?? throw new InvalidOperationException("Requires a logged-in customer.");
        var orders = await db.MaintenanceOrders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders
            .Select(o => new CustomerOrderSummaryResponse(
                o.Reference.Value, o.Status, o.BikeBrand, o.ServiceName, o.DueDate, MoneyResponse.From(o.Total)))
            .ToList();
    }

    /// <summary>
    /// The order, if the caller owns it or knows its email or phone. Anything else is "not found",
    /// so a guessed reference doesn't reveal whether the order exists.
    /// </summary>
    private async Task<MaintenanceOrder> FindAccessibleAsync(string reference, string? contact, CancellationToken ct)
    {
        var notFound = new NotFoundException($"Order '{reference}' was not found.");
        if (!OrderReference.TryParse(reference, out var parsed))
        {
            throw notFound;
        }

        var order = await db.MaintenanceOrders.SingleOrDefaultAsync(o => o.Reference == parsed, ct) ?? throw notFound;
        var isOwner = currentUser.Id is { } userId && order.CustomerId == userId;
        return isOwner || MatchesContact(order.Contact, contact) ? order : throw notFound;
    }

    /// <summary>Compares normalized values, so "+47 912 34 567" matches "91234567" and email case is ignored.</summary>
    private static bool MatchesContact(ContactInfo orderContact, string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
        {
            return false;
        }

        try
        {
            return given.Contains('@')
                ? Email.Create(given) == orderContact.Email
                : PhoneNumber.Create(given) == orderContact.Phone;
        }
        catch (DomainException)
        {
            return false; // not a valid email or phone, so it can't match
        }
    }

    private static CustomerOrderResponse ToResponse(MaintenanceOrder order) => new(
        order.Reference.Value,
        order.Status,
        order.Contact.ToContactResponse(),
        order.BikeBrand,
        order.BikeModel,
        order.ServiceName,
        order.DueDate,
        order.CustomerNotes,
        CanEditNotes: order.Status == OrderStatus.New,
        order.ToPriceResponse(),
        order.CreatedAt);
}
