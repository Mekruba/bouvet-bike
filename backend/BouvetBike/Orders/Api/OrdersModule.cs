using BouvetBike.Orders.Application;
using BouvetBike.Orders.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
        orders.MapGet("/", GetMine).RequireAuthorization(Policies.Customer);
        orders.MapGet("/{reference}", GetByReference).WithName(nameof(GetByReference));
        orders.MapPatch("/{reference}", Patch).Accepts<JsonPatchDocument<CustomerOrderPatch>>(JsonPatch.MediaType);

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

    private static async Task<Ok<IReadOnlyList<CustomerOrderSummaryResponse>>> GetMine(ICustomerOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetMyOrdersAsync(ct));

    /// <param name="contact">Email or phone on the order; guests only.</param>
    private static async Task<Ok<CustomerOrderResponse>> GetByReference(
        string reference,
        [FromHeader(Name = OrderAccess.ContactHeader)] string? contact,
        ICustomerOrderService orders,
        CancellationToken ct) =>
        TypedResults.Ok(await orders.GetAsync(reference, contact, ct));

    /// <param name="contact">Email or phone on the order; guests only.</param>
    private static async Task<Results<Ok<CustomerOrderResponse>, ProblemHttpResult>> Patch(
        string reference,
        [FromHeader(Name = OrderAccess.ContactHeader)] string? contact,
        JsonPatchDocument<CustomerOrderPatch> patch,
        ICustomerOrderService orders,
        CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await orders.PatchAsync(reference, contact, patch, ct));
    }
}
