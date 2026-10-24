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

/// <summary>The customer side of orders: quotes, placing, lookup by reference or contact, and notes.</summary>
public sealed class CustomerOrderService(
    AppDbContext db,
    ICatalogService catalog,
    IDiscountService discounts,
    ICapacityService capacity,
    ICurrentUser currentUser,
    TimeProvider clock) : ICustomerOrderService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<CustomerOrderResponse> GetAsync(string reference, CancellationToken ct) =>
        ToResponse(await FindAsync(reference, ct));

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerOrderSummaryResponse>> FindByContactAsync(string contact, CancellationToken ct)
    {
        var orders = db.MaintenanceOrders.IgnoreAutoIncludes().AsNoTracking();
        if (contact.Contains('@'))
        {
            var email = Email.Create(contact);
            orders = orders.Where(o => o.Contact.Email == email);
        }
        else
        {
            var phone = TryPhone(contact) ?? throw new DomainException($"'{contact}' is not a valid email address or phone number.");
            orders = orders.Where(o => o.Contact.Phone == phone);
        }

        return (await orders.OrderByDescending(o => o.CreatedAt).ToListAsync(ct)).Select(o => o.ToSummaryResponse()).ToList();
    }

    /// <inheritdoc />
    public async Task<CustomerOrderResponse> PatchAsync(string reference, JsonPatchDocument<CustomerOrderPatch> patch, CancellationToken ct)
    {
        var order = await FindAsync(reference, ct);
        var model = new CustomerOrderPatch { Notes = order.CustomerNotes };

        patch.ApplyAndValidate(model);
        try
        {
            order.UpdateCustomerNotes(model.Notes, clock);
        }
        catch (DomainException ex) when (ex is not StateConflictException)
        {
            // RFC 5789: a patch that can't be applied in the resource's current state is 422, not 400. A state conflict passes through as 409.
            throw UnprocessableException.From(ex);
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(order);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerOrderSummaryResponse>> GetMyOrdersAsync(CancellationToken ct)
    {
        var customerId = currentUser.Id ?? throw new InvalidOperationException("Requires a logged-in customer.");
        var orders = await db.MaintenanceOrders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToSummaryResponse()).ToList();
    }

    /// <summary>The order with this reference. The reference is random, so knowing it is the access check.</summary>
    private async Task<MaintenanceOrder> FindAsync(string reference, CancellationToken ct)
    {
        var notFound = new NotFoundException($"Order '{reference}' was not found.");
        return OrderReference.TryParse(reference, out var parsed)
            ? await db.MaintenanceOrders.SingleOrDefaultAsync(o => o.Reference == parsed, ct) ?? throw notFound
            : throw notFound;
    }

    /// <summary>The value as a phone number, or null if it isn't one.</summary>
    private static PhoneNumber? TryPhone(string value)
    {
        try
        {
            return PhoneNumber.Create(value);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    /// <summary>The order as the customer sees it: no internal notes or staff details.</summary>
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
