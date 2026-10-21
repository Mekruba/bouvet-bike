using BouvetBike.Shared.Domain;

namespace BouvetBike.Shared.Application;

/// <summary>
/// The request is well-formed but can't be applied: a JSON Patch with an unknown or read-only path,
/// or one that would leave the resource invalid (RFC 5789 §2.2). Mapped to 422, with field errors when given.
/// </summary>
public class UnprocessableException(
    string message,
    IReadOnlyDictionary<string, string[]>? errors = null,
    IReadOnlyDictionary<string, object?>? details = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();

    /// <summary>Extra fields for the response, e.g. carried over from a <see cref="DomainException"/>.</summary>
    public IReadOnlyDictionary<string, object?> Details { get; } = details ?? new Dictionary<string, object?>();

    /// <summary>A business rule broken by a PATCH: same message and details, but 422 instead of 400.</summary>
    public static UnprocessableException From(DomainException exception) => new(exception.Message, details: exception.Details);
}
