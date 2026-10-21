namespace BouvetBike.Shared.Domain;

/// <summary>
/// Thrown when a business rule is violated. Mapped to a 400 ProblemDetails response;
/// <see cref="Details"/> are added to it as extra fields.
/// </summary>
public class DomainException(string message) : Exception(message)
{
    /// <summary>Machine-readable extras for the client, e.g. a suggested alternative.</summary>
    public IReadOnlyDictionary<string, object?> Details { get; init; } = new Dictionary<string, object?>();
}
