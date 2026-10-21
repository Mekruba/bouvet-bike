namespace BouvetBike.Domain;

/// <summary>
/// Thrown when a business rule is violated. Mapped to a 400 ProblemDetails response
/// by <see cref="Endpoints.DomainExceptionHandler"/>.
/// </summary>
public class DomainException(string message) : Exception(message);
