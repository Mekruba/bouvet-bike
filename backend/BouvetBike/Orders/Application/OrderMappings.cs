using BouvetBike.Catalog.Contracts;
using BouvetBike.Catalog.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Application;

/// <summary>Mapping and lookups shared by the customer and admin order services.</summary>
internal static class OrderMappings
{
    /// <summary>The contact details, with email and phone in their normalized forms.</summary>
    public static ContactResponse ToContactResponse(this ContactInfo contact) =>
        new(contact.Name, contact.Email.Value, contact.Phone.Value);

    /// <summary>One line in a customer's order list.</summary>
    public static CustomerOrderSummaryResponse ToSummaryResponse(this MaintenanceOrder order) =>
        new(order.Reference.Value, order.Status, order.BikeBrand, order.ServiceName, order.DueDate, MoneyResponse.From(order.Total));

    /// <summary>The price breakdown, with the reason when staff overrode the total, so the customer sees why it changed.</summary>
    public static OrderPriceResponse ToPriceResponse(this MaintenanceOrder order) => new(
        MoneyResponse.From(order.ServicePrice),
        order.Discount?.Code,
        MoneyResponse.From(order.DiscountAmount),
        MoneyResponse.From(order.Total),
        IsOverridden: order.PriceOverride is not null,
        OverrideReason: order.PriceOverride?.Reason);

    /// <summary>
    /// A service that can be put on an order. Unknown or inactive is a broken rule, not a missing resource:
    /// the ID came in the request body, so the answer is 400, not 404.
    /// </summary>
    /// <exception cref="DomainException">Unknown or inactive service type.</exception>
    public static async Task<ServiceType> GetOrderableServiceAsync(this ICatalogService catalog, Guid serviceTypeId, CancellationToken ct)
    {
        try
        {
            var service = await catalog.GetForOrderAsync(serviceTypeId, ct);
            return service.IsActive ? service : throw new DomainException($"Service '{service.Name}' is not available.");
        }
        catch (NotFoundException)
        {
            throw new DomainException($"Service type {serviceTypeId} does not exist.");
        }
    }
}
