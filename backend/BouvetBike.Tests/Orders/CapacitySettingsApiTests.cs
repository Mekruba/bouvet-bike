using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>A fresh app, untouched by tests that change the schedule.</summary>
public class CapacityDefaultsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Schedule_StartsAtTheConfiguredCapacity_EveryDay() =>
        Assert.Equal(new CapacityScheduleResponse(5, 5, 5, 5, 5, 5, 5),
            await factory.CreateClient(Roles.Mechanic).GetAsync("/api/admin/capacity/schedule").ReadAs<CapacityScheduleResponse>());
}

/// <summary>
/// Staff capacity settings: the weekday defaults (<c>/api/admin/capacity/schedule</c>, a JSON Patch singleton)
/// and per-date overrides (<c>/api/admin/capacity/overrides/{date}</c>). Today (the test clock) is Monday 2 March 2026.
/// Tests in this class share a database, so each sets the schedule it relies on and uses its own dates.
/// </summary>
public class CapacitySettingsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ScheduleUrl = "/api/admin/capacity/schedule";
    private const string OverridesUrl = "/api/admin/capacity/overrides";
    private static readonly DateOnly NextSaturday = Today.AddDays(5);
    private static readonly DateOnly NextSunday = Today.AddDays(6);

    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);
    private readonly HttpClient _mechanic = factory.CreateClient(Roles.Mechanic);
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);

    // ---- weekday defaults ----

    [Fact]
    public async Task PatchSchedule_ChangesWeekdayDefaults_AndCapacityFollows()
    {
        var schedule = await SetSchedule(saturday: 3, sunday: 0);

        var days = await Capacity(NextSaturday, 2);

        Assert.Equal((3, 0), (schedule.Saturday, schedule.Sunday));
        Assert.Equal((3, 3, false), (days[0].Capacity, days[0].DefaultCapacity, days[0].IsAdjusted));
        Assert.Equal((0, true, true), (days[1].Capacity, days[1].IsClosed, days[1].IsFull));
    }

    [Fact]
    public async Task ClosedWeekday_CantBeBooked_AndSuggestsTheNextOpenDay()
    {
        await SetSchedule(sunday: 0);

        var problem = await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), NextSunday))
            .AssertProblem(HttpStatusCode.BadRequest);

        Assert.Equal($"{NextSunday.AddDays(1):yyyy-MM-dd}", problem.GetProperty("nextAvailableDate").GetString());
    }

    [Theory]
    [InlineData("""[{ "op": "replace", "path": "/saturday", "value": -1 }]""")]
    [InlineData("""[{ "op": "replace", "path": "/holiday", "value": 1 }]""")]
    public async Task PatchSchedule_InvalidValueOrPath_Is422(string patch) =>
        await _admin.PatchJsonAsync(ScheduleUrl, patch).AssertProblem(HttpStatusCode.UnprocessableEntity);

    [Fact]
    public async Task Schedule_IsVisibleToStaff_ButOnlyAdminsChangeIt()
    {
        await _mechanic.GetAsync(ScheduleUrl).AssertStatus(HttpStatusCode.OK);
        await _mechanic.PatchJsonAsync(ScheduleUrl, """[{ "op": "replace", "path": "/monday", "value": 4 }]""").AssertStatus(HttpStatusCode.Forbidden);
        await _anonymous.GetAsync(ScheduleUrl).AssertStatus(HttpStatusCode.Unauthorized);
    }

    // ---- date overrides ----

    [Fact]
    public async Task PutOverride_Is201_ThenReplacing_Is200_AndCapacityShowsItAgainstTheDefault()
    {
        await SetSchedule();
        var date = Today.AddDays(30); // a Wednesday

        var created = await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(6, "Extra mechanic"))
            .ReadAs<CapacityOverrideResponse>(HttpStatusCode.Created);
        var replaced = await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(7, "Two extra"))
            .ReadAs<CapacityOverrideResponse>(HttpStatusCode.OK);

        Assert.Equal(new CapacityOverrideResponse(date, 6, 5, "Extra mechanic"), created);
        Assert.Equal(new CapacityOverrideResponse(date, 7, 5, "Two extra"), replaced);
        var day = (await Capacity(date, 1))[0];
        Assert.Equal((7, 5, true), (day.Capacity, day.DefaultCapacity, day.IsAdjusted));
    }

    [Fact]
    public async Task Override_WithExtraCapacity_TakesMoreBookings()
    {
        await SetSchedule();
        var date = Today.AddDays(31);
        await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(6, null)).AssertStatus(HttpStatusCode.Created);
        var serviceId = await _anonymous.ServiceTypeId();

        await _anonymous.PlaceOrders(serviceId, date, count: 6);

        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(serviceId, date)).AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Override_OfZero_ClosesTheDay()
    {
        await SetSchedule();
        var date = Today.AddDays(60); // the day after must be untouched by other tests: it's the expected suggestion
        await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(0, "Holiday")).AssertStatus(HttpStatusCode.Created);

        var problem = await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), date)).AssertProblem(HttpStatusCode.BadRequest);

        Assert.Equal($"{date.AddDays(1):yyyy-MM-dd}", problem.GetProperty("nextAvailableDate").GetString());
        Assert.True((await Capacity(date, 1))[0].IsClosed);
    }

    [Fact]
    public async Task LoweringBelowWhatsBooked_KeepsTheOrders_AndShowsTheDayFull()
    {
        await SetSchedule();
        var date = Today.AddDays(33);
        await _anonymous.PlaceOrders(await _anonymous.ServiceTypeId(), date, count: 3);

        await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(1, "Short-staffed")).AssertStatus(HttpStatusCode.Created);

        var day = (await Capacity(date, 1))[0];
        Assert.Equal((3, 1, 0, true), (day.Booked, day.Capacity, day.Remaining, day.IsFull));
    }

    [Fact]
    public async Task DeleteOverride_RestoresTheDefault_AndIsIdempotent()
    {
        await SetSchedule();
        var date = Today.AddDays(34);
        await _admin.PutJsonAsync($"{OverridesUrl}/{date:yyyy-MM-dd}", new SetCapacityOverrideRequest(9, null)).AssertStatus(HttpStatusCode.Created);

        await _admin.DeleteAsync($"{OverridesUrl}/{date:yyyy-MM-dd}").AssertStatus(HttpStatusCode.NoContent);
        await _admin.DeleteAsync($"{OverridesUrl}/{date:yyyy-MM-dd}").AssertStatus(HttpStatusCode.NoContent);

        Assert.False((await Capacity(date, 1))[0].IsAdjusted);
    }

    [Fact]
    public async Task Overrides_AreListedByDate_WithinARange()
    {
        await SetSchedule(saturday: 3);
        var saturday = Today.AddDays(40); // 11 April 2026 is a Saturday
        var later = Today.AddDays(45);
        await _admin.PutJsonAsync($"{OverridesUrl}/{later:yyyy-MM-dd}", new SetCapacityOverrideRequest(2, null)).AssertStatus(HttpStatusCode.Created);
        await _admin.PutJsonAsync($"{OverridesUrl}/{saturday:yyyy-MM-dd}", new SetCapacityOverrideRequest(5, "Spring rush")).AssertStatus(HttpStatusCode.Created);

        var overrides = await _mechanic.GetAsync($"{OverridesUrl}?from={saturday:yyyy-MM-dd}&to={later:yyyy-MM-dd}").ReadAs<List<CapacityOverrideResponse>>();

        Assert.Equal([saturday, later], overrides.Select(o => o.Date));
        Assert.Equal(new CapacityOverrideResponse(saturday, 5, 3, "Spring rush"), overrides[0]);
    }

    [Fact]
    public async Task Override_InThePast_Is400() =>
        await _admin.PutJsonAsync($"{OverridesUrl}/{Today.AddDays(-1):yyyy-MM-dd}", new SetCapacityOverrideRequest(6, null))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Override_WithNegativeOrMissingCapacity_Is400_WithFieldError()
    {
        var url = $"{OverridesUrl}/{Today.AddDays(50):yyyy-MM-dd}";

        (await _admin.PutJsonAsync(url, new SetCapacityOverrideRequest(-1, null)).AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("maxOrders");
        (await _admin.PutJsonAsync(url, new { reason = "x" }).AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("maxOrders");
    }

    [Fact]
    public async Task Overrides_AreVisibleToStaff_ButOnlyAdminsChangeThem()
    {
        var url = $"{OverridesUrl}/{Today.AddDays(51):yyyy-MM-dd}";

        await _mechanic.GetAsync(OverridesUrl).AssertStatus(HttpStatusCode.OK);
        await _mechanic.PutJsonAsync(url, new SetCapacityOverrideRequest(6, null)).AssertStatus(HttpStatusCode.Forbidden);
        await _mechanic.DeleteAsync(url).AssertStatus(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private Task<CapacityScheduleResponse> SetSchedule(int saturday = 5, int sunday = 5) =>
        _admin.PatchJsonAsync(ScheduleUrl, $$"""
            [
              { "op": "replace", "path": "/monday", "value": 5 },
              { "op": "replace", "path": "/tuesday", "value": 5 },
              { "op": "replace", "path": "/wednesday", "value": 5 },
              { "op": "replace", "path": "/thursday", "value": 5 },
              { "op": "replace", "path": "/friday", "value": 5 },
              { "op": "replace", "path": "/saturday", "value": {{saturday}} },
              { "op": "replace", "path": "/sunday", "value": {{sunday}} }
            ]
            """).ReadAs<CapacityScheduleResponse>();

    private Task<List<DayCapacityResponse>> Capacity(DateOnly from, int days) =>
        _anonymous.GetAsync($"/api/capacity?from={from:yyyy-MM-dd}&days={days}").ReadAs<List<DayCapacityResponse>>();
}
