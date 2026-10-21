using System.Net;
using BouvetBike.Orders.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// <c>GET /api/capacity</c>: bookings per due date, max 5 per day. Public, for the order form's date picker
/// and the admin overview. Each test uses its own dates, since tests in a class share one database.
/// </summary>
public class CapacityApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/capacity";
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);

    [Fact]
    public async Task ListsDays_WithBookedCounts()
    {
        var day = Today.AddDays(10);
        await _anonymous.PlaceOrders(await _anonymous.ServiceTypeId(), day, count: 2);

        var days = await _anonymous.GetAsync($"{Url}?from={day:yyyy-MM-dd}&days=3").ReadAs<List<DayCapacityResponse>>();

        Assert.Equal([day, day.AddDays(1), day.AddDays(2)], days.Select(d => d.Date));
        Assert.Equal((2, 5, 3, false), (days[0].Booked, days[0].Capacity, days[0].Remaining, days[0].IsFull));
        Assert.Equal(0, days[1].Booked);
    }

    [Fact]
    public async Task FullDay_IsMarkedFull()
    {
        var day = Today.AddDays(20);
        await _anonymous.PlaceOrders(await _anonymous.ServiceTypeId(), day, count: 5);

        var days = await _anonymous.GetAsync($"{Url}?from={day:yyyy-MM-dd}&days=1").ReadAs<List<DayCapacityResponse>>();

        Assert.Equal((5, 0, true), (days[0].Booked, days[0].Remaining, days[0].IsFull));
    }

    [Fact]
    public async Task CancelledOrders_DontCount()
    {
        var day = Today.AddDays(30);
        var order = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), day));
        await factory.CancelOrder(order.Reference);

        var days = await _anonymous.GetAsync($"{Url}?from={day:yyyy-MM-dd}&days=1").ReadAs<List<DayCapacityResponse>>();

        Assert.Equal(0, days[0].Booked);
    }

    [Fact]
    public async Task Defaults_ToTwoWeeksFromToday()
    {
        var days = await _anonymous.GetAsync(Url).ReadAs<List<DayCapacityResponse>>();

        Assert.Equal(14, days.Count);
        Assert.Equal(Today, days[0].Date);
    }

    [Theory]
    [InlineData("days=0")]
    [InlineData("days=61")]
    [InlineData("from=not-a-date")]
    public async Task InvalidQuery_Is400(string query) =>
        await _anonymous.GetAsync($"{Url}?{query}").AssertStatus(HttpStatusCode.BadRequest);
}
