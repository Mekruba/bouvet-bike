namespace BouvetBike.Shared.Application;

/// <summary>The requested resource doesn't exist, or the caller may not see it. Mapped to 404.</summary>
public class NotFoundException(string message) : Exception(message);
