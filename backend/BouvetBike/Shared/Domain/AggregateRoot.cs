namespace BouvetBike.Shared.Domain;

/// <summary>
/// Root of a consistency boundary. Aggregates are loaded and saved as a whole through their root,
/// and refer to other aggregates by ID only.
/// </summary>
public abstract class AggregateRoot : Entity;
