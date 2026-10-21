using BouvetBike.Discounts.Application;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Discounts.Api;

public static class DiscountsModule
{
    public static IServiceCollection AddDiscountsModule(this IServiceCollection services) =>
        services.AddScoped<IDiscountService, DiscountService>();

    /// <summary>Admin management. Customers use codes through orders and price quotes.</summary>
    public static RouteGroupBuilder MapDiscountsEndpoints(this RouteGroupBuilder api)
    {
        var codes = api.MapGroup("/admin/discount-codes").WithTags("Discount codes").RequireAuthorization(Policies.Admin);

        codes.MapGet("/", GetAll);
        codes.MapGet("/{id:guid}", GetDiscountCode).WithName(nameof(GetDiscountCode));
        codes.MapPost("/", Create);
        codes.MapPatch("/{id:guid}", Patch).Accepts<JsonPatchDocument<DiscountCodePatch>>(JsonPatch.MediaType);

        return api;
    }

    private static async Task<Ok<IReadOnlyList<DiscountCodeResponse>>> GetAll(IDiscountService discounts, CancellationToken ct) =>
        TypedResults.Ok(await discounts.GetAllAsync(ct));

    private static async Task<Ok<DiscountCodeResponse>> GetDiscountCode(Guid id, IDiscountService discounts, CancellationToken ct) =>
        TypedResults.Ok(await discounts.GetByIdAsync(id, ct));

    private static async Task<CreatedAtRoute<DiscountCodeResponse>> Create(
        CreateDiscountCodeRequest request, IDiscountService discounts, CancellationToken ct)
    {
        var created = await discounts.CreateAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetDiscountCode), new { id = created.Id });
    }

    private static async Task<Results<Ok<DiscountCodeResponse>, ProblemHttpResult>> Patch(
        Guid id, JsonPatchDocument<DiscountCodePatch> patch, IDiscountService discounts, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await discounts.PatchAsync(id, patch, ct));
    }
}
