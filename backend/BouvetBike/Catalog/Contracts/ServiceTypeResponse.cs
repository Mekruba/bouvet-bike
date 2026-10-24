using BouvetBike.Shared.Contracts;

namespace BouvetBike.Catalog.Contracts;

/// <summary>A service type in the catalog.</summary>
/// <param name="IsActive">False once retired: it can't be ordered any more, but existing orders keep it.</param>
public sealed record ServiceTypeResponse(
    Guid Id,
    string Name,
    string? Description,
    MoneyResponse Price,
    int EstimatedMinutes,
    bool IsActive);
