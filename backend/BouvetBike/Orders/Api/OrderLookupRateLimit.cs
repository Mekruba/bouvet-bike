using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BouvetBike.Orders.Api;

/// <summary>Bound from "RateLimiting:OrderLookups".</summary>
public sealed class OrderLookupLimitOptions
{
    public const string Section = "RateLimiting:OrderLookups";

    [Range(1, 1_000_000)]
    public int PermitsPerMinute { get; init; } = 30;
}

/// <summary>
/// A reference, an email or a phone number is all it takes to see orders, so lookups are limited per client IP
/// (fixed one-minute window). Over the limit: 429 with Retry-After and a ProblemDetails body.
/// </summary>
public static class OrderLookupRateLimit
{
    public const string Policy = "order-lookup";

    public static IServiceCollection AddOrderLookupRateLimit(this IServiceCollection services)
    {
        services.AddOptions<OrderLookupLimitOptions>().BindConfiguration(OrderLookupLimitOptions.Section).ValidateDataAnnotations().ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<OrderLookupLimitOptions>>().Value.PermitsPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
            options.OnRejected = async (rejected, ct) =>
            {
                var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
                rejected.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                await rejected.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = rejected.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many lookups",
                        Detail = "Too many order lookups from this connection. Try again in a minute.",
                    },
                });
            };
        });

        return services;
    }
}
