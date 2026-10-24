using BouvetBike.Discounts.Application;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Discounts.Api;

/// <summary>The Discounts module's wiring: services and the admin <c>/api/admin/discount-codes</c> endpoints.</summary>
public static class DiscountsModule
{
    /// <summary>Registers the discount service. Called from <c>Program.cs</c>.</summary>
    public static IServiceCollection AddDiscountsModule(this IServiceCollection services) =>
        services.AddScoped<IDiscountService, DiscountService>();

    /// <summary>
    /// Maps <c>/api/admin/discount-codes</c> (Admin only). Customers use codes through orders and price quotes.
    /// There's no DELETE (405): orders keep the code they used, so retire a code instead.
    /// </summary>
    public static RouteGroupBuilder MapDiscountsEndpoints(this RouteGroupBuilder api)
    {
        var codes = api.MapGroup("/admin/discount-codes").WithTags("Discount codes").RequireAuthorization(Policies.Admin);

        codes.MapGet("/", GetAll);
        codes.MapGet("/{id:guid}", GetDiscountCode).WithName(nameof(GetDiscountCode));
        codes.MapPost("/", Create);
        codes.MapPatch("/{id:guid}", Patch).Accepts<JsonPatchDocument<DiscountCodePatch>>(JsonPatch.MediaType);

        return api;
    }

    /// <summary>List the discount codes</summary>
    /// <remarks>Every code, active or not, sorted by code, with how often each has been used.</remarks>
    internal static async Task<Ok<IReadOnlyList<DiscountCodeResponse>>> GetAll(IDiscountService discounts, CancellationToken ct) =>
        TypedResults.Ok(await discounts.GetAllAsync(ct));

    /// <summary>Get a discount code</summary>
    /// <remarks>404 if unknown.</remarks>
    internal static async Task<Ok<DiscountCodeResponse>> GetDiscountCode(Guid id, IDiscountService discounts, CancellationToken ct) =>
        TypedResults.Ok(await discounts.GetByIdAsync(id, ct));

    /// <summary>Add a discount code</summary>
    /// <remarks>201 with the new code and its <c>Location</c>; 409 if the code exists (ignoring case); 400 for invalid terms, e.g. a percentage over 100.</remarks>
    internal static async Task<CreatedAtRoute<DiscountCodeResponse>> Create(
        CreateDiscountCodeRequest request, IDiscountService discounts, CancellationToken ct)
    {
        var created = await discounts.CreateAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetDiscountCode), new { id = created.Id });
    }

    /// <summary>Change or retire a discount code</summary>
    /// <remarks>
    /// A JSON Patch document on <c>/type</c>, <c>/value</c>, <c>/validFrom</c>, <c>/validTo</c>, <c>/maxUses</c>
    /// or <c>/isActive</c>. <c>/code</c> is read-only, since orders keep the code they used. 200 with the updated code.
    /// 400 malformed patch, 409 failed <c>test</c>, 422 bad or read-only path or invalid terms.
    /// </remarks>
    internal static async Task<Results<Ok<DiscountCodeResponse>, ProblemHttpResult>> Patch(
        Guid id, JsonPatchDocument<DiscountCodePatch> patch, IDiscountService discounts, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await discounts.PatchAsync(id, patch, ct));
    }
}
