using BouvetBike.Orders.Application;
using BouvetBike.Orders.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BouvetBike.Orders.Api;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddOptions<CapacityOptions>().BindConfiguration(CapacityOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<ICapacityService, CapacityService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        return services;
    }

    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/capacity", GetCapacity).WithTags("Orders");
        api.MapGet("/price-quotes", GetPriceQuote).WithTags("Orders");

        var orders = api.MapGroup("/orders").WithTags("Orders");
        orders.MapPost("/", Place);
        orders.MapGet("/{reference}", GetByReference).WithName(nameof(GetByReference));

        return api;
    }

    private static async Task<Ok<IReadOnlyList<DayCapacityResponse>>> GetCapacity(
        [AsParameters] CapacityQuery query, ICapacityService capacity, CancellationToken ct) =>
        TypedResults.Ok(await capacity.GetAsync(query, ct));

    private static async Task<Ok<OrderPriceResponse>> GetPriceQuote(
        [AsParameters] PriceQuoteQuery query, ICustomerOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.QuoteAsync(query, ct));

    private static async Task<CreatedAtRoute<CustomerOrderResponse>> Place(
        PlaceOrderRequest request, ICustomerOrderService orders, CancellationToken ct)
    {
        var order = await orders.PlaceAsync(request, ct);
        return TypedResults.CreatedAtRoute(order, nameof(GetByReference), new { reference = order.Reference });
    }

    /// <param name="contact">Email or phone on the order; guests only.</param>
    private static async Task<Ok<CustomerOrderResponse>> GetByReference(
        string reference,
        [FromHeader(Name = OrderAccess.ContactHeader)] string? contact,
        ICustomerOrderService orders,
        CancellationToken ct) =>
        TypedResults.Ok(await orders.GetAsync(reference, contact, ct));
}
