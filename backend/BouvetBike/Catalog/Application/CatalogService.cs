using BouvetBike.Catalog.Contracts;
using BouvetBike.Catalog.Domain;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Catalog.Application;

/// <summary>The catalog, stored in <see cref="AppDbContext.ServiceTypes"/>.</summary>
public sealed class CatalogService(AppDbContext db) : ICatalogService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceTypeResponse>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var services = await db.ServiceTypes
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name) // NOCASE collation: case-insensitive order
            .ToListAsync(ct);
        return services.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<ServiceTypeResponse> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToResponse(await GetForOrderAsync(id, ct));

    /// <inheritdoc />
    public async Task<ServiceTypeResponse> CreateAsync(CreateServiceTypeRequest request, CancellationToken ct)
    {
        await EnsureNameIsFree(request.Name, exceptId: null, ct);
        var service = ServiceType.Create(
            request.Name, request.Description, new Money(request.Price!.Value), TimeSpan.FromMinutes(request.EstimatedMinutes!.Value));

        db.ServiceTypes.Add(service);
        await db.SaveChangesAsync(ct);
        return ToResponse(service);
    }

    /// <inheritdoc />
    public async Task<ServiceTypeResponse> PatchAsync(Guid id, JsonPatchDocument<ServiceTypePatch> patch, CancellationToken ct)
    {
        var service = await GetForOrderAsync(id, ct);
        var model = new ServiceTypePatch
        {
            Name = service.Name,
            Description = service.Description,
            Price = service.BasePrice.Amount,
            EstimatedMinutes = (int)service.EstimatedDuration.TotalMinutes,
            IsActive = service.IsActive,
        };

        patch.ApplyAndValidate(model);
        await EnsureNameIsFree(model.Name, exceptId: id, ct);

        try
        {
            service.Update(model.Name, model.Description, new Money(model.Price), TimeSpan.FromMinutes(model.EstimatedMinutes));
        }
        catch (DomainException ex) when (ex is not StateConflictException)
        {
            // RFC 5789: a patch that would leave the resource invalid is 422, not 400. A state conflict passes through as 409.
            throw UnprocessableException.From(ex);
        }

        if (model.IsActive)
        {
            service.Activate();
        }
        else
        {
            service.Deactivate();
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(service);
    }

    /// <inheritdoc />
    public async Task<ServiceType> GetForOrderAsync(Guid id, CancellationToken ct) =>
        await db.ServiceTypes.FindAsync([id], ct) ?? throw new NotFoundException($"Service type {id} was not found.");

    /// <summary>Service names are unique, ignoring case (the column uses the NOCASE collation).</summary>
    /// <param name="exceptId">The service being renamed, which may keep its own name.</param>
    /// <exception cref="ConflictException">Another service has the name.</exception>
    private async Task EnsureNameIsFree(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.ServiceTypes.AnyAsync(s => s.Name == trimmed && s.Id != exceptId, ct))
        {
            throw new ConflictException($"A service type named '{trimmed}' already exists.");
        }
    }

    /// <summary>The service as the API shows it, with the duration in whole minutes.</summary>
    private static ServiceTypeResponse ToResponse(ServiceType service) => new(
        service.Id,
        service.Name,
        service.Description,
        MoneyResponse.From(service.BasePrice),
        (int)service.EstimatedDuration.TotalMinutes,
        service.IsActive);
}
