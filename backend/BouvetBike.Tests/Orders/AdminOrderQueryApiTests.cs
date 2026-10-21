using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.AdminOrderTestHelpers;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// Staff view of orders: <c>GET /api/admin/orders</c> (search, filter, sort, page) and <c>GET /api/admin/orders/{id}</c>.
/// Each test tags its orders with a unique bike brand and searches for it, since tests share a database.
/// </summary>
public class AdminOrderQueryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _mechanic = factory.CreateClient(Roles.Mechanic);

    // ---- access ----

    [Fact]
    public async Task Search_RequiresLogin() =>
        await _anonymous.GetAsync(AdminOrdersUrl).AssertStatus(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Search_IsForStaffOnly() =>
        await factory.CreateClient(Roles.Customer).GetAsync(AdminOrdersUrl).AssertStatus(HttpStatusCode.Forbidden);

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Mechanic)]
    public async Task Search_IsOpenToAllStaff(string role) =>
        await factory.CreateClient(role).GetAsync(AdminOrdersUrl).AssertStatus(HttpStatusCode.OK);

    // ---- search ----

    [Fact]
    public async Task Search_ByBikeBrand_FindsOnlyMatchingOrders()
    {
        var brand = UniqueBrand();
        await Place(brand: brand);
        await Place(brand: brand);
        await Place();

        var result = await _mechanic.Search($"search={brand}");

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, o => Assert.Equal(brand, o.BikeBrand));
    }

    [Fact]
    public async Task Search_ByCustomerName_IsPartialAndCaseInsensitive()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var order = await Place(name: $"Ola Nordmann{unique}");

        var result = await _mechanic.Search($"search=NORDMANN{unique.ToUpperInvariant()}");

        Assert.Equal(order.Reference, Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Search_ByReference_AnyCase_PrefixOptional()
    {
        var order = await Place();

        var result = await _mechanic.Search($"search={order.Reference[3..].ToLowerInvariant()}");

        Assert.Equal(order.Reference, Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Search_ByEmail_IgnoresCase()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var order = await Place(email: email);

        var result = await _mechanic.Search($"search={Uri.EscapeDataString(email.ToUpperInvariant())}");

        Assert.Equal(order.Reference, Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Search_ByPhone_InAnyFormat()
    {
        var order = await Place(phone: "+47 400 12 345");

        var result = await _mechanic.Search($"search={Uri.EscapeDataString("40012345")}");

        Assert.Contains(result.Items, o => o.Reference == order.Reference);
        Assert.All(result.Items, o => Assert.Equal("+4740012345", o.Contact.Phone));
    }

    // ---- filters ----

    [Fact]
    public async Task Filter_ByStatus()
    {
        var brand = UniqueBrand();
        var cancelled = await Place(brand: brand);
        await Place(brand: brand);
        await factory.CancelOrder(cancelled.Reference);

        var result = await _mechanic.Search($"search={brand}&status=Cancelled");

        Assert.Equal(cancelled.Reference, Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task Filter_ByDueDateRange_IsInclusive()
    {
        var brand = UniqueBrand();
        var day = FreshDay();
        await Place(brand: brand, dueDate: day.AddDays(-1));
        var first = await Place(brand: brand, dueDate: day);
        var last = await Place(brand: brand, dueDate: day.AddDays(1));
        await Place(brand: brand, dueDate: day.AddDays(2));

        var result = await _mechanic.Search($"search={brand}&dueFrom={day:yyyy-MM-dd}&dueTo={day.AddDays(1):yyyy-MM-dd}");

        Assert.Equal([first.Reference, last.Reference], result.Items.Select(o => o.Reference).Order());
    }

    [Fact]
    public async Task Filter_ByServiceType()
    {
        var brand = UniqueBrand();
        var brakeId = await _anonymous.ServiceTypeId("Brake maintenance");
        var brakes = await Place(brand: brand, serviceTypeId: brakeId);
        await Place(brand: brand);

        var result = await _mechanic.Search($"search={brand}&serviceTypeId={brakeId}");

        Assert.Equal(brakes.Reference, Assert.Single(result.Items).Reference);
    }

    // ---- sorting and paging ----

    [Fact]
    public async Task Sort_DefaultsToDueDateAscending_AndCanBeReversed()
    {
        var brand = UniqueBrand();
        var later = await Place(brand: brand, dueDate: FreshDay().AddDays(5));
        var sooner = await Place(brand: brand, dueDate: FreshDay());

        var ascending = await _mechanic.Search($"search={brand}");
        var descending = await _mechanic.Search($"search={brand}&sort=DueDate&descending=true");

        Assert.Equal([sooner.Reference, later.Reference], ascending.Items.Select(o => o.Reference));
        Assert.Equal([later.Reference, sooner.Reference], descending.Items.Select(o => o.Reference));
    }

    [Fact]
    public async Task Sort_ByCustomerName()
    {
        var brand = UniqueBrand();
        var zed = await Place(brand: brand, name: "Zed");
        var anne = await Place(brand: brand, name: "anne");

        var result = await _mechanic.Search($"search={brand}&sort=CustomerName");

        Assert.Equal([anne.Reference, zed.Reference], result.Items.Select(o => o.Reference));
    }

    [Fact]
    public async Task Paging_ReturnsPagesAndTotals()
    {
        var brand = UniqueBrand();
        for (var i = 0; i < 3; i++)
        {
            await Place(brand: brand);
        }

        var page1 = await _mechanic.Search($"search={brand}&pageSize=2&page=1");
        var page2 = await _mechanic.Search($"search={brand}&pageSize=2&page=2");

        Assert.Equal((2, 3, 2, 1), (page1.Items.Count, page1.TotalCount, page1.TotalPages, page1.Page));
        Assert.Single(page2.Items);
        Assert.Empty(page1.Items.Select(o => o.Id).Intersect(page2.Items.Select(o => o.Id)));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=Bogus")]
    [InlineData("sort=Bogus")]
    [InlineData("dueFrom=not-a-date")]
    public async Task InvalidQuery_Is400(string query) =>
        await _mechanic.GetAsync($"{AdminOrdersUrl}?{query}").AssertStatus(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Summary_ShowsContactGuestFlagAndTotal()
    {
        var brand = UniqueBrand();
        var guestOrder = await Place(brand: brand);
        var customerOrder = await factory.CreateClient(Roles.Customer).PlaceOrder(
            NewOrder(await _anonymous.ServiceTypeId(), FreshDay()) with { BikeBrand = brand });

        var result = await _mechanic.Search($"search={brand}");

        var guest = Assert.Single(result.Items, o => o.Reference == guestOrder.Reference);
        Assert.Equal((true, new ContactResponse("Kari Nordmann", "kari@example.com", "+4791234567"), new MoneyResponse(790, "NOK")), (guest.IsGuest, guest.Contact, guest.Total));
        Assert.False(Assert.Single(result.Items, o => o.Reference == customerOrder.Reference).IsGuest);
    }

    // ---- GET one ----

    [Fact]
    public async Task Get_ShowsEverythingStaffNeed()
    {
        var placed = await Place(notes: "Squeaks");
        var id = await _mechanic.AdminId(placed);

        var order = await _mechanic.GetAdminOrder(id);

        Assert.Equal((placed.Reference, OrderStatus.New, "Squeaks", null as string), (order.Reference, order.Status, order.CustomerNotes, order.InternalNotes));
        Assert.Equal([OrderStatus.InProgress, OrderStatus.WaitingForCustomer, OrderStatus.Cancelled], order.AllowedStatusChanges);
        Assert.Equal((null as Guid?, "Chain replacement", new MoneyResponse(790, "NOK")), (order.CustomerId, order.ServiceName, order.Price.Total));
        Assert.Empty(order.StatusHistory);
        Assert.Null(order.PriceOverride);
    }

    [Fact]
    public async Task Get_ShowsStatusHistory()
    {
        var placed = await Place();
        await factory.SetStatus(placed.Reference, OrderStatus.InProgress);

        var order = await _mechanic.GetAdminOrder(await _mechanic.AdminId(placed));

        var change = Assert.Single(order.StatusHistory);
        Assert.Equal((OrderStatus.New, OrderStatus.InProgress), (change.From, change.To));
        Assert.Equal([OrderStatus.WaitingForCustomer, OrderStatus.Completed, OrderStatus.Cancelled], order.AllowedStatusChanges);
    }

    [Fact]
    public async Task Get_Unknown_Is404() =>
        await _mechanic.GetAsync($"{AdminOrdersUrl}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task Get_IsForStaffOnly() =>
        await factory.CreateClient(Roles.Customer).GetAsync($"{AdminOrdersUrl}/{Guid.NewGuid()}").AssertStatus(HttpStatusCode.Forbidden);

    // ---- helpers ----

    private async Task<CustomerOrderResponse> Place(
        string brand = "Trek",
        string name = "Kari Nordmann",
        string email = "kari@example.com",
        string phone = "912 34 567",
        DateOnly? dueDate = null,
        Guid? serviceTypeId = null,
        string? notes = null) =>
        await _anonymous.PlaceOrder(
            NewOrder(serviceTypeId ?? await _anonymous.ServiceTypeId(), dueDate ?? FreshDay(), email: email, phone: phone, notes: notes)
                with { BikeBrand = brand, Name = name });
}
