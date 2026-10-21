using BouvetBike.Shared.Contracts;

namespace BouvetBike.Catalog.Contracts;

public sealed record ServiceTypeResponse(Guid Id, string Name, string? Description, MoneyResponse Price, int EstimatedMinutes);
