using BouvetBike.Orders.Application;
using BouvetBike.Orders.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Shared.Contracts;
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
        services.AddScoped<IAdminOrderService, AdminOrderService>();
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

        // The staff view of the same orders: separate routes, staff-only.
        var admin = api.MapGroup("/admin/orders").WithTags("Admin orders").RequireAuthorization(Policies.Staff);
        admin.MapGet("/", Search);
        admin.MapGet("/{id:guid}", GetForStaff);
        admin.MapPatch("/{id:guid}", PatchForStaff).Accepts<JsonPatchDocument<AdminOrderPatch>>(JsonPatch.MediaType);
        admin.MapDelete("/{id:guid}", Delete).RequireAuthorization(Policies.Admin);

        // The lifecycle: status changes are a history sub-collection; the price override is an Admin-only singleton.
        admin.MapGet("/{id:guid}/status-changes", GetStatusChanges);
        admin.MapGet("/{id:guid}/status-changes/{changeId:guid}", GetStatusChange).WithName(nameof(GetStatusChange));
        admin.MapPost("/{id:guid}/status-changes", AddStatusChange);
        admin.MapGet("/{id:guid}/price-override", GetPriceOverride).WithName(nameof(GetPriceOverride));
        admin.MapPut("/{id:guid}/price-override", SetPriceOverride).RequireAuthorization(Policies.Admin);
        admin.MapDelete("/{id:guid}/price-override", ClearPriceOverride).RequireAuthorization(Policies.Admin);

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

    private static async Task<Ok<PagedResponse<AdminOrderSummaryResponse>>> Search(
        [AsParameters] AdminOrderQuery query, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.SearchAsync(query, ct));

    private static async Task<Ok<AdminOrderResponse>> GetForStaff(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetAsync(id, ct));

    private static async Task<Results<Ok<AdminOrderResponse>, ProblemHttpResult>> PatchForStaff(
        Guid id, JsonPatchDocument<AdminOrderPatch> patch, IAdminOrderService orders, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await orders.PatchAsync(id, patch, ct));
    }

    private static async Task<NoContent> Delete(Guid id, IAdminOrderService orders, CancellationToken ct)
    {
        await orders.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<StatusChangeResponse>>> GetStatusChanges(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetStatusChangesAsync(id, ct));

    private static async Task<Ok<StatusChangeResponse>> GetStatusChange(Guid id, Guid changeId, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetStatusChangeAsync(id, changeId, ct));

    private static async Task<CreatedAtRoute<StatusChangeResponse>> AddStatusChange(
        Guid id, ChangeStatusRequest request, IAdminOrderService orders, CancellationToken ct)
    {
        var change = await orders.AddStatusChangeAsync(id, request, ct);
        return TypedResults.CreatedAtRoute(change, nameof(GetStatusChange), new { id, changeId = change.Id });
    }

    private static async Task<Ok<PriceOverrideResponse>> GetPriceOverride(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetPriceOverrideAsync(id, ct));

    /// <summary>RFC 9110: 201 when the override is created, 200 when an existing one is replaced.</summary>
    private static async Task<Results<CreatedAtRoute<PriceOverrideResponse>, Ok<PriceOverrideResponse>>> SetPriceOverride(
        Guid id, OverridePriceRequest request, IAdminOrderService orders, CancellationToken ct)
    {
        var (priceOverride, created) = await orders.SetPriceOverrideAsync(id, request, ct);
        return created
            ? TypedResults.CreatedAtRoute(priceOverride, nameof(GetPriceOverride), new { id })
            : TypedResults.Ok(priceOverride);
    }

    private static async Task<NoContent> ClearPriceOverride(Guid id, IAdminOrderService orders, CancellationToken ct)
    {
        await orders.ClearPriceOverrideAsync(id, ct);
        return TypedResults.NoContent();
    }
}
