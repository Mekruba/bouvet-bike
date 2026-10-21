using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;

namespace BouvetBike.Shared.Api;

/// <summary>HTTP-level JSON Patch (RFC 6902) handling shared by all PATCH endpoints.</summary>
public static class JsonPatch
{
    public const string MediaType = "application/json-patch+json";

    /// <summary>
    /// A 400 problem if the document has an operation RFC 6902 doesn't define, or no path.
    /// That makes the document malformed (RFC 5789: 400), as opposed to valid but inapplicable (422).
    /// </summary>
    public static ProblemHttpResult? FindMalformedOperation<T>(JsonPatchDocument<T> patch) where T : class
    {
        var bad = patch.Operations.FirstOrDefault(o => o.OperationType == OperationType.Invalid || string.IsNullOrEmpty(o.path));
        return bad is null
            ? null
            : TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Malformed JSON Patch document",
                detail: $"'{bad.op}' at '{bad.path}' is not a valid RFC 6902 operation.");
    }
}
