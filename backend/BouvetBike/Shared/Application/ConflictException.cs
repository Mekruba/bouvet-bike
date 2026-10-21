namespace BouvetBike.Shared.Application;

/// <summary>The request conflicts with the resource's current state (duplicate, failed JSON Patch <c>test</c>). Mapped to 409.</summary>
public class ConflictException(string message) : Exception(message);
