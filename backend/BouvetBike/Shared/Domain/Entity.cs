namespace BouvetBike.Shared.Domain;

public abstract class Entity
{
    /// <summary>Generated in the domain (UUIDv7, time-ordered) so an entity has its identity before it is saved.</summary>
    public Guid Id { get; private init; } = Guid.CreateVersion7();
}
