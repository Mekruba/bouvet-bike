using BouvetBike.Catalog.Domain;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Catalog.Contracts;

/// <summary>The service catalog: what the shop offers and at what price.</summary>
public interface ICatalogService
{
    /// <summary>Sorted by name. Inactive services are only included when asked for (staff only).</summary>
    Task<IReadOnlyList<ServiceTypeResponse>> GetAllAsync(bool includeInactive, CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">Unknown service type.</exception>
    Task<ServiceTypeResponse> GetByIdAsync(Guid id, CancellationToken ct);

    /// <exception cref="Shared.Application.ConflictException">A service type with that name exists (case-insensitive).</exception>
    Task<ServiceTypeResponse> CreateAsync(CreateServiceTypeRequest request, CancellationToken ct);

    /// <summary>Applies an RFC 6902 JSON Patch atomically: if any operation fails, nothing changes.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown service type.</exception>
    /// <exception cref="Shared.Application.ConflictException">A <c>test</c> operation failed, or the new name is taken.</exception>
    /// <exception cref="Shared.Application.UnprocessableException">Unknown or read-only path, or the patched values are invalid.</exception>
    Task<ServiceTypeResponse> PatchAsync(Guid id, JsonPatchDocument<ServiceTypePatch> patch, CancellationToken ct);

    /// <summary>For other modules (Orders) that need the service itself.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown service type.</exception>
    Task<ServiceType> GetForOrderAsync(Guid id, CancellationToken ct);
}
