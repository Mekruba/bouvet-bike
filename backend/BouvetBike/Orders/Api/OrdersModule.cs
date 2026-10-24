using System.Security.Claims;
using BouvetBike.Orders.Application;
using BouvetBike.Orders.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Shared.Contracts;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;

namespace BouvetBike.Orders.Api;

/// <summary>The Orders module's wiring: services, the customer and staff order endpoints, and capacity.</summary>
public static class OrdersModule
{
    /// <summary>
    /// Registers the order and capacity services, the guest order linker (listening to Users),
    /// and the lookup rate limit. Capacity options are validated on startup. Called from <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddOptions<CapacityOptions>().BindConfiguration(CapacityOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<ICapacityService, CapacityService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<ICustomerAccountEvents, GuestOrderLinker>();
        services.AddOrderLookupRateLimit();
        return services;
    }

    /// <summary>
    /// Maps the public endpoints (<c>/api/capacity</c>, <c>/api/price-quotes</c>, <c>/api/orders</c>) and the staff ones
    /// (<c>/api/admin/orders</c>, <c>/api/admin/capacity</c>). Order responses carry personal data, so they're never cached.
    /// </summary>
    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/capacity", GetCapacity).WithTags("Orders");
        api.MapGet("/price-quotes", GetPriceQuote).WithTags("Orders");

        var orders = api.MapGroup("/orders").WithTags("Orders").WithNoStore();
        orders.MapPost("/", Place);
        // Anyone with a reference, email or phone can see orders, so these lookups are rate-limited.
        orders.MapGet("/", List).RequireRateLimiting(OrderLookupRateLimit.Policy);
        orders.MapGet("/{reference}", GetByReference).WithName(nameof(GetByReference)).RequireRateLimiting(OrderLookupRateLimit.Policy);
        orders.MapPatch("/{reference}", Patch)
            .Accepts<JsonPatchDocument<CustomerOrderPatch>>(JsonPatch.MediaType)
            .RequireRateLimiting(OrderLookupRateLimit.Policy);

        // The staff view of the same orders: separate routes, staff-only.
        var admin = api.MapGroup("/admin/orders").WithTags("Admin orders").RequireAuthorization(Policies.Staff).WithNoStore();
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

        // Capacity settings: weekday defaults (a singleton) and per-date overrides keyed by date.
        var capacity = api.MapGroup("/admin/capacity").WithTags("Capacity").RequireAuthorization(Policies.Staff);
        capacity.MapGet("/schedule", GetSchedule);
        capacity.MapPatch("/schedule", PatchSchedule)
            .RequireAuthorization(Policies.Admin)
            .Accepts<JsonPatchDocument<CapacitySchedulePatch>>(JsonPatch.MediaType);
        capacity.MapGet("/overrides", GetOverrides);
        capacity.MapGet("/overrides/{date}", GetOverride).WithName(nameof(GetOverride));
        capacity.MapPut("/overrides/{date}", SetOverride).RequireAuthorization(Policies.Admin);
        capacity.MapDelete("/overrides/{date}", RemoveOverride).RequireAuthorization(Policies.Admin);

        return api;
    }

    /// <summary>Get bookings and capacity per day</summary>
    /// <remarks>For the date picker. From today for 14 days by default (max 60); each day has its capacity and weekday default.</remarks>
    internal static async Task<Ok<IReadOnlyList<DayCapacityResponse>>> GetCapacity(
        [AsParameters] CapacityQuery query, ICapacityService capacity, CancellationToken ct) =>
        TypedResults.Ok(await capacity.GetAsync(query, ct));

    /// <summary>Get a price quote</summary>
    /// <remarks>What an order would cost, with an optional discount code. Never uses up the code. 400 for a retired service or an unusable code.</remarks>
    internal static async Task<Ok<OrderPriceResponse>> GetPriceQuote(
        [AsParameters] PriceQuoteQuery query, ICustomerOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.QuoteAsync(query, ct));

    /// <summary>Place an order</summary>
    /// <remarks>
    /// As a guest or a logged-in customer (then it's linked to the account). 201 with the order, <c>Location: /api/orders/{reference}</c>.
    /// 400 for a full due date (with <c>nextAvailableDate</c>), a retired service or an unusable discount code.
    /// </remarks>
    internal static async Task<CreatedAtRoute<CustomerOrderResponse>> Place(
        PlaceOrderRequest request, ICustomerOrderService orders, CancellationToken ct)
    {
        var order = await orders.PlaceAsync(request, ct);
        return TypedResults.CreatedAtRoute(order, nameof(GetByReference), new { reference = order.Reference });
    }

    /// <summary>Find orders by email or phone, or list my orders</summary>
    /// <remarks>
    /// With the <c>Order-Contact</c> header: every order with that email or phone, newest first (guest lookup; rate-limited).
    /// Without it: the logged-in customer's own orders; 401 when not logged in, 403 for staff.
    /// </remarks>
    internal static async Task<Results<Ok<IReadOnlyList<CustomerOrderSummaryResponse>>, ChallengeHttpResult, ForbidHttpResult>> List(
        [FromHeader(Name = OrderAccess.ContactHeader)] string? contact,
        ClaimsPrincipal user,
        IAuthorizationService authorization,
        ICustomerOrderService orders,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(contact))
        {
            return TypedResults.Ok(await orders.FindByContactAsync(contact.Trim(), ct));
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Challenge();
        }

        return (await authorization.AuthorizeAsync(user, Policies.Customer)).Succeeded
            ? TypedResults.Ok(await orders.GetMyOrdersAsync(ct))
            : TypedResults.Forbid();
    }

    /// <summary>Get an order by its reference</summary>
    /// <remarks>The reference is the key: knowing it is enough. With or without the <c>BB-</c> prefix, any case. 404 if no order has it. Rate-limited.</remarks>
    internal static async Task<Ok<CustomerOrderResponse>> GetByReference(string reference, ICustomerOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetAsync(reference, ct));

    /// <summary>Change my notes on an order</summary>
    /// <remarks>
    /// A JSON Patch document on <c>/notes</c>. 200 with the order. 409 once the repair has started;
    /// 400 malformed patch, 422 any other path or invalid notes. Rate-limited.
    /// </remarks>
    internal static async Task<Results<Ok<CustomerOrderResponse>, ProblemHttpResult>> Patch(
        string reference,
        JsonPatchDocument<CustomerOrderPatch> patch,
        ICustomerOrderService orders,
        CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await orders.PatchAsync(reference, patch, ct));
    }

    /// <summary>Search orders</summary>
    /// <remarks>
    /// Staff only. One page of orders. The search matches reference, email or phone exactly, and name or bike brand
    /// as part of the text; filter by status, service and due dates; sort by due date, creation time, name or status.
    /// </remarks>
    internal static async Task<Ok<PagedResponse<AdminOrderSummaryResponse>>> Search(
        [AsParameters] AdminOrderQuery query, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.SearchAsync(query, ct));

    /// <summary>Get an order (staff view)</summary>
    /// <remarks>Staff only. The full order: internal notes, price override, status history and the status moves allowed now. 404 if unknown.</remarks>
    internal static async Task<Ok<AdminOrderResponse>> GetForStaff(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetAsync(id, ct));

    /// <summary>Change an order</summary>
    /// <remarks>
    /// Staff only. A JSON Patch document on the customer and bike details, <c>/serviceTypeId</c>, <c>/dueDate</c>,
    /// <c>/internalNotes</c> or <c>/discountCode</c>. 200 with the order. 409 when the order's status forbids the change
    /// or a <c>test</c> fails; 422 bad path, invalid values or a full due date.
    /// </remarks>
    internal static async Task<Results<Ok<AdminOrderResponse>, ProblemHttpResult>> PatchForStaff(
        Guid id, JsonPatchDocument<AdminOrderPatch> patch, IAdminOrderService orders, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await orders.PatchAsync(id, patch, ct));
    }

    /// <summary>Delete an order</summary>
    /// <remarks>Admin only. Deletes the order and its history. 204; 404 if unknown.</remarks>
    internal static async Task<NoContent> Delete(Guid id, IAdminOrderService orders, CancellationToken ct)
    {
        await orders.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    /// <summary>List an order's status changes</summary>
    /// <remarks>Staff only. The status history, oldest first, with who changed it and why.</remarks>
    internal static async Task<Ok<IReadOnlyList<StatusChangeResponse>>> GetStatusChanges(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetStatusChangesAsync(id, ct));

    /// <summary>Get a status change</summary>
    /// <remarks>Staff only. One history entry. 404 if the order or the entry is unknown.</remarks>
    internal static async Task<Ok<StatusChangeResponse>> GetStatusChange(Guid id, Guid changeId, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetStatusChangeAsync(id, changeId, ct));

    /// <summary>Change an order's status</summary>
    /// <remarks>
    /// Staff only. Moves the order to a new status, with an optional reason.
    /// 201 with the history entry and its <c>Location</c>; 409 when the current status doesn't allow that move.
    /// </remarks>
    internal static async Task<CreatedAtRoute<StatusChangeResponse>> AddStatusChange(
        Guid id, ChangeStatusRequest request, IAdminOrderService orders, CancellationToken ct)
    {
        var change = await orders.AddStatusChangeAsync(id, request, ct);
        return TypedResults.CreatedAtRoute(change, nameof(GetStatusChange), new { id, changeId = change.Id });
    }

    /// <summary>Get an order's price override</summary>
    /// <remarks>Staff only. The final price an admin set, with who, when and why. 404 when none is set.</remarks>
    internal static async Task<Ok<PriceOverrideResponse>> GetPriceOverride(Guid id, IAdminOrderService orders, CancellationToken ct) =>
        TypedResults.Ok(await orders.GetPriceOverrideAsync(id, ct));

    /// <summary>Set an order's final price</summary>
    /// <remarks>
    /// Admin only. The reason is shown to the customer. 201 when the override is created, 200 when an existing one
    /// is replaced (RFC 9110); 409 for a cancelled order.
    /// </remarks>
    internal static async Task<Results<CreatedAtRoute<PriceOverrideResponse>, Ok<PriceOverrideResponse>>> SetPriceOverride(
        Guid id, OverridePriceRequest request, IAdminOrderService orders, CancellationToken ct)
    {
        var (priceOverride, created) = await orders.SetPriceOverrideAsync(id, request, ct);
        return created
            ? TypedResults.CreatedAtRoute(priceOverride, nameof(GetPriceOverride), new { id })
            : TypedResults.Ok(priceOverride);
    }

    /// <summary>Remove an order's price override</summary>
    /// <remarks>Admin only. Back to the calculated price. 204, also when none was set; 409 for a cancelled order.</remarks>
    internal static async Task<NoContent> ClearPriceOverride(Guid id, IAdminOrderService orders, CancellationToken ct)
    {
        await orders.ClearPriceOverrideAsync(id, ct);
        return TypedResults.NoContent();
    }

    /// <summary>Get the weekday capacity defaults</summary>
    /// <remarks>Staff only. The default number of orders for each weekday; 0 means closed.</remarks>
    internal static async Task<Ok<CapacityScheduleResponse>> GetSchedule(ICapacityService capacity, CancellationToken ct) =>
        TypedResults.Ok(await capacity.GetScheduleAsync(ct));

    /// <summary>Change the weekday capacity defaults</summary>
    /// <remarks>
    /// Admin only. A JSON Patch document on <c>/monday</c> … <c>/sunday</c>. 200 with the schedule.
    /// 400 malformed patch, 409 failed <c>test</c>, 422 bad path or invalid capacity.
    /// </remarks>
    internal static async Task<Results<Ok<CapacityScheduleResponse>, ProblemHttpResult>> PatchSchedule(
        JsonPatchDocument<CapacitySchedulePatch> patch, ICapacityService capacity, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await capacity.PatchScheduleAsync(patch, ct));
    }

    /// <summary>List the date capacity overrides</summary>
    /// <remarks>Staff only. The dates in the range whose capacity differs from their weekday default; from today for 60 days by default.</remarks>
    internal static async Task<Ok<IReadOnlyList<CapacityOverrideResponse>>> GetOverrides(
        [AsParameters] CapacityOverrideQuery query, ICapacityService capacity, CancellationToken ct) =>
        TypedResults.Ok(await capacity.GetOverridesAsync(query, ct));

    /// <summary>Get a date's capacity override</summary>
    /// <remarks>Staff only. 404 when the date uses its weekday default.</remarks>
    internal static async Task<Results<Ok<CapacityOverrideResponse>, NotFound>> GetOverride(DateOnly date, ICapacityService capacity, CancellationToken ct) =>
        (await capacity.GetOverridesAsync(new CapacityOverrideQuery(date, date), ct)).FirstOrDefault() is { } found
            ? TypedResults.Ok(found)
            : TypedResults.NotFound();

    /// <summary>Set a date's capacity</summary>
    /// <remarks>
    /// Admin only. More or less capacity than the weekday default, or 0 to close the date. 201 when the date gets
    /// its first override, 200 when it's replaced (RFC 9110); 400 for a past date.
    /// </remarks>
    internal static async Task<Results<CreatedAtRoute<CapacityOverrideResponse>, Ok<CapacityOverrideResponse>>> SetOverride(
        DateOnly date, SetCapacityOverrideRequest request, ICapacityService capacity, CancellationToken ct)
    {
        var (result, created) = await capacity.SetOverrideAsync(date, request, ct);
        return created
            ? TypedResults.CreatedAtRoute(result, nameof(GetOverride), new { date = date.ToString("yyyy-MM-dd") })
            : TypedResults.Ok(result);
    }

    /// <summary>Remove a date's capacity override</summary>
    /// <remarks>Admin only. The date goes back to its weekday default. 204, also when it had none.</remarks>
    internal static async Task<NoContent> RemoveOverride(DateOnly date, ICapacityService capacity, CancellationToken ct)
    {
        await capacity.RemoveOverrideAsync(date, ct);
        return TypedResults.NoContent();
    }
}
