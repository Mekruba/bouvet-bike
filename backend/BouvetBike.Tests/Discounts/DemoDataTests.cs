using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Tests.Orders;
using Microsoft.AspNetCore.Hosting;

namespace BouvetBike.Tests.Discounts;

/// <summary>The app as started with <c>just dev</c> (Development environment), which seeds demo data.</summary>
public class DevelopmentApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}

/// <summary>Demo discount codes exist in Development, so the discount features can be shown, and nowhere else.</summary>
public class DemoDataTests(DevelopmentApiFactory development, ApiFactory testing)
    : IClassFixture<DevelopmentApiFactory>, IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("WELCOME10", 79)] // 10 % of 790
    [InlineData("SPRING100", 100)] // fixed 100 NOK
    public async Task Development_HasDemoDiscountCodes(string code, decimal discount)
    {
        var client = development.CreateClient(role: null);
        var serviceId = await client.ServiceTypeId("Chain replacement");

        var quote = await client.GetAsync($"/api/price-quotes?serviceTypeId={serviceId}&discountCode={code}").ReadAs<OrderPriceResponse>();

        Assert.Equal(discount, quote.DiscountAmount.Amount);
    }

    [Fact]
    public async Task OtherEnvironments_HaveNoDemoData()
    {
        var client = testing.CreateClient(role: null);
        var serviceId = await client.ServiceTypeId();

        await client.GetAsync($"/api/price-quotes?serviceTypeId={serviceId}&discountCode=WELCOME10").AssertProblem(HttpStatusCode.BadRequest);
    }
}
