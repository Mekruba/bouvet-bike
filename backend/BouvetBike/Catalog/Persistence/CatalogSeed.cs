using BouvetBike.Catalog.Domain;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Catalog.Persistence;

/// <summary>The services from the assignment. Prices and durations are placeholders.</summary>
public static class CatalogSeed
{
    /// <summary>New instances on every call, ready to be added to a context.</summary>
    public static IEnumerable<ServiceType> ServiceTypes() =>
    [
        ServiceType.Create("Wheel adjustment", "Truing, spoke tension and hub check.", new Money(490), TimeSpan.FromMinutes(30)),
        ServiceType.Create("Chain replacement", "New chain, fitted and lubricated.", new Money(790), TimeSpan.FromMinutes(45)),
        ServiceType.Create("Brake maintenance", "Pads, cables or bleed, and adjustment.", new Money(690), TimeSpan.FromMinutes(60)),
    ];
}
