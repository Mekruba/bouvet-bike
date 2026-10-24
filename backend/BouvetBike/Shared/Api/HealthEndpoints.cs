namespace BouvetBike.Shared.Api;

/// <summary>A liveness check for scripts and container health checks.</summary>
public static class HealthEndpoints
{
    /// <summary>Maps <c>GET /api/health</c>: 200 with <c>{ "status": "ok" }</c> while the app is running.</summary>
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
        return api;
    }
}
