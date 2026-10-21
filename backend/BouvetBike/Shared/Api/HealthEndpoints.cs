namespace BouvetBike.Shared.Api;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
        return api;
    }
}
