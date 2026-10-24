namespace BouvetBike.Shared.Domain;

/// <summary>Base for domain objects with an identity that outlives changes to their data.</summary>
public abstract class Entity
{
    /// <summary>Generated in the domain (UUIDv7, time-ordered) so an entity has its identity before it is saved.</summary>
    public Guid Id { get; private init; } = Guid.CreateVersion7();
}
