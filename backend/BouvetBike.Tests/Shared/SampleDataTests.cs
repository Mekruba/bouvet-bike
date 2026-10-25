using System.Net;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;
using BouvetBike.Tests.Orders;
using BouvetBike.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BouvetBike.Tests.Shared;

/// <summary>
/// <c>SeedSampleData=true</c> fills a new database with orders, so the app looks like it's being used.
/// It never touches a database that already exists. Each test starts its own app, since the flag is read on startup.
/// </summary>
public class SampleDataTests
{
    private const string AdminOrders = AdminOrderTestHelpers.AdminOrdersUrl;

    [Fact]
    public async Task WithTheFlag_ANewDatabaseLooksUsed()
    {
        using var factory = new ApiFactory();
        using var app = WithSampleData(factory);
        var admin = Client(app, Roles.Admin);

        var all = await admin.GetAsync($"{AdminOrders}?pageSize=100").ReadAs<PagedResponse<AdminOrderSummaryResponse>>();

        Assert.True(all.TotalCount >= 20, $"Expected at least 20 sample orders, got {all.TotalCount}.");
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            Assert.Contains(all.Items, o => o.Status == status);
        }

        Assert.Contains(all.Items, o => !o.IsGuest); // some orders belong to a customer account
        Assert.Contains(all.Items, o => o.DueDate < TestData.Today); // a history, not just upcoming work
        Assert.Contains(all.Items, o => o.DueDate > TestData.Today);

        var details = await Task.WhenAll(all.Items.Select(o => admin.GetAsync($"{AdminOrders}/{o.Id}").ReadAs<AdminOrderResponse>()));
        Assert.Contains(details, o => o.PriceOverride is { Reason.Length: > 0 });
        Assert.Contains(details, o => o.StatusHistory.Any(c => c.ChangedBy is not null)); // changes by named staff
        Assert.Contains(details, o => o.InternalNotes is not null);
        Assert.Contains(details, o => o.Price.DiscountCode is not null);

        var codes = await admin.GetAsync("/api/admin/discount-codes").ReadAs<List<DiscountCodeResponse>>();
        Assert.Contains(codes, c => c.TimesUsed > 0);

        var overrides = await admin.GetAsync("/api/admin/capacity/overrides").ReadAs<List<CapacityOverrideResponse>>();
        Assert.Contains(overrides, o => o.MaxOrders == 0); // a closed day
        Assert.Contains(overrides, o => o.MaxOrders > o.DefaultCapacity); // a day with extra capacity
    }

    [Fact]
    public async Task WithTheFlag_TheSampleData_RespectsCapacity()
    {
        using var factory = new ApiFactory();
        using var app = WithSampleData(factory);

        var days = await Client(app, role: null).GetAsync($"/api/capacity?from={TestData.Today:yyyy-MM-dd}&days=14").ReadAs<List<DayCapacityResponse>>();

        Assert.All(days, d => Assert.True(d.Booked <= d.Capacity, $"{d.Date}: {d.Booked} booked, capacity {d.Capacity}"));
        Assert.Contains(days, d => d.IsFull && !d.IsClosed); // a full day, to show the next-available suggestion
        Assert.Contains(days, d => d.Booked > 0 && !d.IsFull);
    }

    [Fact]
    public async Task WithoutTheFlag_ANewDatabaseHasNoOrders()
    {
        using var factory = new ApiFactory();

        var all = await Client(factory, Roles.Admin).GetAsync(AdminOrders).ReadAs<PagedResponse<AdminOrderSummaryResponse>>();

        Assert.Equal(0, all.TotalCount);
    }

    [Fact]
    public async Task WithTheFlag_AnExistingDatabase_IsLeftAlone()
    {
        using var factory = new ApiFactory();
        Client(factory, Roles.Admin); // starts the app, which creates the database, without the flag

        using var restarted = WithSampleData(factory); // same database file, now with the flag
        var all = await Client(restarted, Roles.Admin).GetAsync(AdminOrders).ReadAs<PagedResponse<AdminOrderSummaryResponse>>();

        Assert.Equal(0, all.TotalCount);
    }

    private static WebApplicationFactory<Program> WithSampleData(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("SeedSampleData", "true"));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? role)
    {
        var client = factory.CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        }

        return client;
    }
}
