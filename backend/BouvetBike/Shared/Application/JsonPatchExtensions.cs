using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Exceptions;

namespace BouvetBike.Shared.Application;

/// <summary>Applying JSON Patch documents to patch models, with RFC 5789 error semantics.</summary>
public static class JsonPatchExtensions
{
    /// <summary>
    /// Applies an RFC 6902 patch to <paramref name="model"/> and validates the result (DataAnnotations).
    /// The caller copies the model into the domain only after this succeeds, so a failed patch changes nothing.
    /// </summary>
    /// <exception cref="ConflictException">A <c>test</c> operation failed (RFC 5789: 409).</exception>
    /// <exception cref="UnprocessableException">An operation couldn't be applied (e.g. unknown path), or the result is invalid (RFC 5789: 422).</exception>
    public static void ApplyAndValidate<T>(this JsonPatchDocument<T> patch, T model) where T : class
    {
        JsonPatchError? error = null;
        try
        {
            patch.ApplyTo(model, e => error ??= e);
        }
        catch (JsonPatchException ex)
        {
            error ??= new JsonPatchError(model, ex.FailedOperation, ex.Message);
        }

        if (error is not null)
        {
            if (error.Operation?.op == "test")
            {
                throw new ConflictException($"The test operation failed: {error.ErrorMessage}");
            }

            throw new UnprocessableException(
                error.ErrorMessage,
                new Dictionary<string, string[]> { [FieldName(error.Operation?.path)] = [error.ErrorMessage] });
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => r.MemberNames.DefaultIfEmpty("").Select(member => (Field: FieldName(member), Message: r.ErrorMessage ?? "Invalid value.")))
                .GroupBy(e => e.Field, e => e.Message)
                .ToDictionary(g => g.Key, g => g.ToArray());
            throw new UnprocessableException("The patched resource is invalid.", errors);
        }
    }

    /// <summary>"/price" or "Price" → "price", matching the JSON property and patch path.</summary>
    private static string FieldName(string? pathOrMember) =>
        JsonNamingPolicy.CamelCase.ConvertName(pathOrMember?.TrimStart('/') ?? "");
}
