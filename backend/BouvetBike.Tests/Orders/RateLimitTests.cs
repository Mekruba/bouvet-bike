using System.Net;
using BouvetBike.Orders.Contracts;
using Microsoft.AspNetCore.Hosting;

namespace BouvetBike.Tests.Orders;

/// <summary>The app with a low lookup limit, to test the throttling without slowing down other tests.</summary>
public class RateLimitedApiFactory : ApiFactory
{
    public const int Limit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:OrderLookups:PermitsPerMinute", Limit.ToString());
    }
}

/// <summary>
/// Order lookups (by reference, or by email or phone) are rate-limited per client, since a reference or a
/// contact is all it takes to see an order. Over the limit: 429 with Retry-After.
/// </summary>
public class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Lookups_OverTheLimit_Are429_WithRetryAfter()
    {
        var client = factory.CreateClient(role: null);
        for (var i = 0; i < RateLimitedApiFactory.Limit; i++)
        {
            await client.GetAsync("/api/orders", (OrderAccess.ContactHeader, "nobody@example.com")).AssertStatus(HttpStatusCode.OK);
        }

        var response = await client.GetAsync("/api/orders/BB-23456789");

        await response.AssertProblem(HttpStatusCode.TooManyRequests);
        Assert.NotNull(response.Headers.RetryAfter);
    }
}
