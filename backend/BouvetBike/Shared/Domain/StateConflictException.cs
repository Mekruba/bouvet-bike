namespace BouvetBike.Shared.Domain;

/// <summary>
/// The operation is valid in itself, but the aggregate's current state forbids it, and a different state would
/// allow it (e.g. completing an order that hasn't been started). Mapped to 409 Conflict: the client should
/// reload the resource rather than change its input.
/// </summary>
public class StateConflictException(string message) : DomainException(message);
