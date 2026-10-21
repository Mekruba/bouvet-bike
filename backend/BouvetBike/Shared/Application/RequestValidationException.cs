namespace BouvetBike.Shared.Application;

/// <summary>
/// Input that only the application can reject (e.g. a password that fails Identity's rules), reported like
/// request validation: 400 with field errors.
/// </summary>
public class RequestValidationException(string message, IReadOnlyDictionary<string, string[]> errors) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
