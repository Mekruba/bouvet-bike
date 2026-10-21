using System.Security.Claims;
using BouvetBike.Catalog.Application;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Catalog.Api;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services) =>
        services.AddScoped<ICatalogService, CatalogService>();

    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var serviceTypes = api.MapGroup("/service-types").WithTags("Catalog");

        serviceTypes.MapGet("/", GetAll);
        serviceTypes.MapGet("/{id:guid}", GetById).WithName(nameof(GetById));
        serviceTypes.MapPost("/", Create).RequireAuthorization(Policies.Admin);
        serviceTypes.MapPatch("/{id:guid}", Patch)
            .RequireAuthorization(Policies.Admin)
            .Accepts<JsonPatchDocument<ServiceTypePatch>>(JsonPatch.MediaType);

        return api;
    }

    /// <param name="includeInactive">Staff only: also list retired service types.</param>
    private static async Task<Results<Ok<IReadOnlyList<ServiceTypeResponse>>, ChallengeHttpResult, ForbidHttpResult>> GetAll(
        bool? includeInactive, ClaimsPrincipal user, IAuthorizationService authorization, ICatalogService catalog, CancellationToken ct)
    {
        if (includeInactive == true && !(await authorization.AuthorizeAsync(user, Policies.Staff)).Succeeded)
        {
            return user.Identity?.IsAuthenticated == true ? TypedResults.Forbid() : TypedResults.Challenge();
        }

        return TypedResults.Ok(await catalog.GetAllAsync(includeInactive == true, ct));
    }

    private static async Task<Ok<ServiceTypeResponse>> GetById(Guid id, ICatalogService catalog, CancellationToken ct) =>
        TypedResults.Ok(await catalog.GetByIdAsync(id, ct));

    private static async Task<CreatedAtRoute<ServiceTypeResponse>> Create(
        CreateServiceTypeRequest request, ICatalogService catalog, CancellationToken ct)
    {
        var created = await catalog.CreateAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetById), new { id = created.Id });
    }

    private static async Task<Results<Ok<ServiceTypeResponse>, ProblemHttpResult>> Patch(
        Guid id, JsonPatchDocument<ServiceTypePatch> patch, ICatalogService catalog, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await catalog.PatchAsync(id, patch, ct));
    }
}
