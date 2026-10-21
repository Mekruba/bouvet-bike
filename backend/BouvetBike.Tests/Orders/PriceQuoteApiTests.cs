using System.Net;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Shared.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// <c>GET /api/price-quotes</c>: the estimated price before ordering (the change request).
/// A safe GET: it checks a discount code but never uses it up.
/// </summary>
public class PriceQuoteApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/price-quotes";
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);

    [Fact]
    public async Task Quote_WithoutDiscount_IsTheServicePrice()
    {
        var serviceId = await _anonymous.ServiceTypeId("Chain replacement");

        var quote = await _anonymous.GetAsync($"{Url}?serviceTypeId={serviceId}").ReadAs<OrderPriceResponse>();

        Assert.Equal(new MoneyResponse(790, "NOK"), quote.ServicePrice);
        Assert.Equal(new MoneyResponse(0, "NOK"), quote.DiscountAmount);
        Assert.Equal(new MoneyResponse(790, "NOK"), quote.Total);
        Assert.Null(quote.DiscountCode);
        Assert.False(quote.IsOverridden);
    }

    [Fact]
    public async Task Quote_WithDiscountCode_ShowsDiscount_CaseInsensitive()
    {
        await factory.SeedDiscountCode("QUOTE20", DiscountType.Percentage, 20);
        var serviceId = await _anonymous.ServiceTypeId("Chain replacement");

        var quote = await _anonymous.GetAsync($"{Url}?serviceTypeId={serviceId}&discountCode=quote20").ReadAs<OrderPriceResponse>();

        Assert.Equal(("QUOTE20", 158m, 632m), (quote.DiscountCode, quote.DiscountAmount.Amount, quote.Total.Amount));
    }

    [Fact]
    public async Task Quote_DoesNotUseUpTheCode()
    {
        await factory.SeedDiscountCode("ONCEONLY", DiscountType.FixedAmount, 100, maxUses: 1);
        var serviceId = await _anonymous.ServiceTypeId();

        await _anonymous.GetAsync($"{Url}?serviceTypeId={serviceId}&discountCode=ONCEONLY").AssertStatus(HttpStatusCode.OK);
        await _anonymous.GetAsync($"{Url}?serviceTypeId={serviceId}&discountCode=ONCEONLY").AssertStatus(HttpStatusCode.OK);

        var order = await _anonymous.PlaceOrder(NewOrder(serviceId, Today.AddDays(1), discountCode: "ONCEONLY"));
        Assert.Equal("ONCEONLY", order.Price.DiscountCode);
    }

    [Fact]
    public async Task Quote_WithUnknownDiscountCode_Is400()
    {
        var serviceId = await _anonymous.ServiceTypeId();

        await _anonymous.GetAsync($"{Url}?serviceTypeId={serviceId}&discountCode=NOPE").AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Quote_WithoutServiceType_Is400_WithFieldError() =>
        (await _anonymous.GetAsync(Url).AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("serviceTypeId");

    [Fact]
    public async Task Quote_WithUnknownServiceType_Is400() =>
        await _anonymous.GetAsync($"{Url}?serviceTypeId={Guid.NewGuid()}").AssertProblem(HttpStatusCode.BadRequest);
}
