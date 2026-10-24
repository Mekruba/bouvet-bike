using System.Security.Claims;
using BouvetBike.Catalog.Application;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Shared.Api;
using BouvetBike.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Catalog.Api;

/// <summary>The Catalog module's wiring: services and the <c>/api/service-types</c> endpoints.</summary>
public static class CatalogModule
{
    /// <summary>Registers the catalog service. Called from <c>Program.cs</c>.</summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services) =>
        services.AddScoped<ICatalogService, CatalogService>();

    /// <summary>Maps <c>/api/service-types</c>: anyone can read the catalog; only admins change it. There's no DELETE (405): retire a service instead.</summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var serviceTypes = api.MapGroup("/service-types").WithTags("Catalog");

        serviceTypes.MapGet("/", GetAll);
        serviceTypes.MapGet("/{id:guid}", GetServiceType).WithName(nameof(GetServiceType));
        serviceTypes.MapPost("/", Create).RequireAuthorization(Policies.Admin);
        serviceTypes.MapPatch("/{id:guid}", Patch)
            .RequireAuthorization(Policies.Admin)
            .Accepts<JsonPatchDocument<ServiceTypePatch>>(JsonPatch.MediaType);

        return api;
    }

    /// <summary>List the services</summary>
    /// <remarks>Active services, sorted by name. Asking for retired ones too needs staff: 401 when not logged in, 403 otherwise.</remarks>
    /// <param name="includeInactive">Staff only: also list retired service types.</param>
    internal static async Task<Results<Ok<IReadOnlyList<ServiceTypeResponse>>, ChallengeHttpResult, ForbidHttpResult>> GetAll(
        bool? includeInactive, ClaimsPrincipal user, IAuthorizationService authorization, ICatalogService catalog, CancellationToken ct)
    {
        if (includeInactive == true && !(await authorization.AuthorizeAsync(user, Policies.Staff)).Succeeded)
        {
            return user.Identity?.IsAuthenticated == true ? TypedResults.Forbid() : TypedResults.Challenge();
        }

        return TypedResults.Ok(await catalog.GetAllAsync(includeInactive == true, ct));
    }

    /// <summary>Get a service</summary>
    /// <remarks>Active or not, since orders still show retired ones. 404 if unknown.</remarks>
    internal static async Task<Ok<ServiceTypeResponse>> GetServiceType(Guid id, ICatalogService catalog, CancellationToken ct) =>
        TypedResults.Ok(await catalog.GetByIdAsync(id, ct));

    /// <summary>Add a service</summary>
    /// <remarks>Admin only. 201 with the new service and its <c>Location</c>; 409 if the name is taken (ignoring case).</remarks>
    internal static async Task<CreatedAtRoute<ServiceTypeResponse>> Create(
        CreateServiceTypeRequest request, ICatalogService catalog, CancellationToken ct)
    {
        var created = await catalog.CreateAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetServiceType), new { id = created.Id });
    }

    /// <summary>Change or retire a service</summary>
    /// <remarks>
    /// Admin only. A JSON Patch document on <c>/name</c>, <c>/description</c>, <c>/price</c>, <c>/estimatedMinutes</c>
    /// or <c>/isActive</c>; set <c>/isActive</c> to false to retire it. 200 with the updated service.
    /// 400 malformed patch, 409 failed <c>test</c> or taken name, 422 bad path or invalid result.
    /// </remarks>
    internal static async Task<Results<Ok<ServiceTypeResponse>, ProblemHttpResult>> Patch(
        Guid id, JsonPatchDocument<ServiceTypePatch> patch, ICatalogService catalog, CancellationToken ct)
    {
        if (JsonPatch.FindMalformedOperation(patch) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await catalog.PatchAsync(id, patch, ct));
    }
}
