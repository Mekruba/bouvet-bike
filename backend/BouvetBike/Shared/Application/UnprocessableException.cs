namespace BouvetBike.Shared.Application;

/// <summary>
/// The request is well-formed but can't be applied: a JSON Patch with an unknown or read-only path,
/// or one that would leave the resource invalid (RFC 5789 §2.2). Mapped to 422, with field errors when given.
/// </summary>
public class UnprocessableException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
