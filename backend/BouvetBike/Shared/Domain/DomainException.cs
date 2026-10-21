namespace BouvetBike.Shared.Domain;

/// <summary>
/// Thrown when a business rule is violated. Mapped to a 400 ProblemDetails response
/// by <c>DomainExceptionHandler</c>.
/// </summary>
public class DomainException(string message) : Exception(message);
