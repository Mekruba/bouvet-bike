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
    public static ContactResponse ToContactResponse(this ContactInfo contact) =>
        new(contact.Name, contact.Email.Value, contact.Phone.Value);

    public static CustomerOrderSummaryResponse ToSummaryResponse(this MaintenanceOrder order) =>
        new(order.Reference.Value, order.Status, order.BikeBrand, order.ServiceName, order.DueDate, MoneyResponse.From(order.Total));

    public static OrderPriceResponse ToPriceResponse(this MaintenanceOrder order) => new(
        MoneyResponse.From(order.ServicePrice),
        order.Discount?.Code,
        MoneyResponse.From(order.DiscountAmount),
        MoneyResponse.From(order.Total),
        IsOverridden: order.PriceOverride is not null);

    /// <summary>A service that can be put on an order. Unknown or inactive is a broken rule, not a missing resource.</summary>
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
