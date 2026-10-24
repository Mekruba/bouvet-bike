using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace BouvetBike.Tests.Shared;

/// <summary>
/// The OpenAPI document is only served in Development (<c>/openapi/v1.json</c>, used by Scalar),
/// so these tests build it directly from the app's services.
/// </summary>
public class OpenApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Document_IsGeneratedForEveryEndpoint()
    {
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        var document = await provider.GetOpenApiDocumentAsync();

        Assert.Contains("/api/capacity", document.Paths.Keys);
        Assert.Contains("/api/price-quotes", document.Paths.Keys);
        Assert.Contains(document.Paths.Keys, path => path.TrimEnd('/') == "/api/admin/orders");
    }
}
