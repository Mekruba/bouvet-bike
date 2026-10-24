namespace BouvetBike.Shared.Api;

/// <summary>HTTP caching rules for endpoints.</summary>
public static class CacheControl
{
    /// <summary>
    /// <c>Cache-Control: no-store</c> (RFC 9111) on the endpoints' responses, for personal data no browser or shared cache may keep.
    /// It also covers responses that depend on a request header or the login cookie (e.g. <c>GET /api/orders</c>), which a cache could otherwise hand to the wrong person.
    /// </summary>
    public static TBuilder WithNoStore<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
}
