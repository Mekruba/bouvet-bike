using BouvetBike.Shared.Application;
using BouvetBike.Shared.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BouvetBike.Shared.Api;

/// <summary>
/// Maps expected exceptions to ProblemDetails: <see cref="NotFoundException"/> → 404,
/// <see cref="DomainException"/> → 400 with its <see cref="DomainException.Details"/> as extra fields.
/// Anything else falls through to the default 500 handler.
/// </summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = exception.Message,
            },
            DomainException domain => WithExtensions(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Business rule violated",
                    Detail = exception.Message,
                },
                domain.Details),
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

    private static ProblemDetails WithExtensions(ProblemDetails problem, IReadOnlyDictionary<string, object?> extensions)
    {
        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }
}
