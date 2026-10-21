using BouvetBike.Catalog.Domain;

namespace BouvetBike.Catalog.Contracts;

public interface ICatalogService
{
    /// <summary>Services customers can order, for the order form.</summary>
    Task<IReadOnlyList<ServiceTypeResponse>> GetActiveAsync(CancellationToken ct);

    /// <summary>For other modules (Orders) that need the service itself.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown service.</exception>
    Task<ServiceType> GetAsync(Guid id, CancellationToken ct);
}
