using BouvetBike.Shared.Application;
using BouvetBike.Shared.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BouvetBike.Shared.Api;

/// <summary>
/// Maps expected exceptions to ProblemDetails:
/// <see cref="NotFoundException"/> → 404, <see cref="ConflictException"/> → 409,
/// <see cref="UnprocessableException"/> → 422 with field errors,
/// <see cref="DomainException"/> → 400 with its <see cref="DomainException.Details"/> as extra fields,
/// and a malformed request (<see cref="BadHttpRequestException"/>) → its own status code.
/// Anything else falls through to the default 500 handler.
/// </summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception),
            UnprocessableException unprocessable => WithExtensions(
                new HttpValidationProblemDetails(unprocessable.Errors.ToDictionary())
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Unprocessable request",
                    Detail = exception.Message,
                },
                unprocessable.Details),
            DomainException domain => WithExtensions(Problem(StatusCodes.Status400BadRequest, "Business rule violated", exception), domain.Details),
            BadHttpRequestException badRequest => Problem(badRequest.StatusCode, "Bad request", exception),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails Problem(int status, string title, Exception exception) =>
        new() { Status = status, Title = title, Detail = exception.Message };

    private static ProblemDetails WithExtensions(ProblemDetails problem, IReadOnlyDictionary<string, object?> extensions)
    {
        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }
}
