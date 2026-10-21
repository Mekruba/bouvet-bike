using System.Net;
using System.Text;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> (place an order, guest or logged in) and <c>GET /api/orders/{reference}</c>
/// (the confirmation and lookup). Guests prove access with the <c>Order-Contact</c> header.
/// </summary>
public class PlaceOrderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _customer = factory.CreateClient(Roles.Customer);

    // ---- placing ----

    [Fact]
    public async Task Place_AsGuest_Returns201_WithConfirmation()
    {
        var serviceId = await _anonymous.ServiceTypeId("Chain replacement");
        var dueDate = FreshDay();
        var request = NewOrder(serviceId, dueDate, email: "Kari@Example.COM", phone: "912 34 567", notes: "Squeaky chain");

        var response = await _anonymous.PostJsonAsync(OrdersUrl, request);
        var order = await response.ReadAs<CustomerOrderResponse>(HttpStatusCode.Created);

        Assert.Matches("^BB-[2-9A-HJKMNP-Z]{8}$", order.Reference);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(new ContactResponse("Kari Nordmann", "kari@example.com", "+4791234567"), order.Contact);
        Assert.Equal(("Trek", "Domane SL5", "Chain replacement"), (order.BikeBrand, order.BikeModel, order.ServiceName));
        Assert.Equal((dueDate, "Squeaky chain", true), (order.DueDate, order.Notes, order.CanEditNotes));
        Assert.Equal(new MoneyResponse(790, "NOK"), order.Price.Total);
        // RFC 9110 allows an absolute or relative Location; what matters is that it points at the new order.
        Assert.EndsWith($"/api/orders/{order.Reference}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Place_Location_ReturnsTheOrder()
    {
        var response = await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), FreshDay()));
        var placed = await response.ReadAs<CustomerOrderResponse>(HttpStatusCode.Created);

        var fetched = await _anonymous.GetAsync(response.Headers.Location!.OriginalString)
            .ReadAs<CustomerOrderResponse>();

        Assert.Equal(placed, fetched);
    }

    [Fact]
    public async Task Place_WithDiscountCode_AppliesIt()
    {
        await factory.SeedDiscountCode("SPRING10", DiscountType.Percentage, 10);

        var order = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId("Chain replacement"), FreshDay(), discountCode: "spring10"));

        Assert.Equal(("SPRING10", 79m, 711m), (order.Price.DiscountCode, order.Price.DiscountAmount.Amount, order.Price.Total.Amount));
    }

    [Fact]
    public async Task Place_WithUsedUpDiscountCode_Is400()
    {
        await factory.SeedDiscountCode("SINGLE", DiscountType.Percentage, 10, maxUses: 1);
        var serviceId = await _anonymous.ServiceTypeId();
        await _anonymous.PlaceOrder(NewOrder(serviceId, FreshDay(), discountCode: "SINGLE"));

        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(serviceId, FreshDay(), discountCode: "SINGLE"))
            .AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Place_Failing_ChangesNothing()
    {
        var day = Today.AddDays(40);
        var serviceId = await _anonymous.ServiceTypeId();

        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(serviceId, day, discountCode: "DOES-NOT-EXIST"))
            .AssertProblem(HttpStatusCode.BadRequest);

        var days = await _anonymous.GetAsync($"/api/capacity?from={day:yyyy-MM-dd}&days=1").ReadAs<List<DayCapacityResponse>>();
        Assert.Equal(0, days[0].Booked);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("bikeBrand")]
    [InlineData("serviceTypeId")]
    [InlineData("dueDate")]
    public async Task Place_WithMissingField_Is400_WithFieldError(string field)
    {
        var body = $$"""
            {
              "name": "Kari Nordmann", "email": "kari@example.com", "phone": "91234567", "bikeBrand": "Trek",
              "serviceTypeId": "{{await _anonymous.ServiceTypeId()}}", "dueDate": "{{Today.AddDays(1):yyyy-MM-dd}}"
            }
            """;
        var withoutField = System.Text.Json.Nodes.JsonNode.Parse(body)!.AsObject();
        withoutField.Remove(field);

        var response = await _anonymous.PostAsync(OrdersUrl, new StringContent(withoutField.ToJsonString(), Encoding.UTF8, "application/json"));

        (await response.AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError(field);
    }

    [Fact]
    public async Task Place_WithInvalidEmail_Is400_WithFieldError() =>
        (await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), Today, email: "not-an-email"))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("email");

    [Fact]
    public async Task Place_WithInvalidPhone_Is400() =>
        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), Today, phone: "1234"))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Place_WithPastDueDate_Is400() =>
        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), Today.AddDays(-1)))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Place_WithUnknownServiceType_Is400() =>
        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(Guid.NewGuid(), Today))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Place_WithInactiveServiceType_Is400()
    {
        var admin = factory.CreateClient(Roles.Admin);
        var retired = await admin.PostJsonAsync("/api/service-types", new CreateServiceTypeRequest("Retired for orders", null, 100, 30))
            .ReadAs<ServiceTypeResponse>(HttpStatusCode.Created);
        await admin.PatchJsonAsync($"/api/service-types/{retired.Id}", """[{ "op": "replace", "path": "/isActive", "value": false }]""")
            .AssertStatus(HttpStatusCode.OK);

        await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(retired.Id, Today)).AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Place_OnFullDate_Is400_WithNextAvailableDate()
    {
        var serviceId = await _anonymous.ServiceTypeId();
        var full = Today.AddDays(50);
        await _anonymous.PlaceOrders(serviceId, full, count: 5);
        await _anonymous.PlaceOrders(serviceId, full.AddDays(1), count: 5);

        var problem = await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(serviceId, full)).AssertProblem(HttpStatusCode.BadRequest);

        Assert.Equal($"{full.AddDays(2):yyyy-MM-dd}", problem.GetProperty("nextAvailableDate").GetString());
    }

    [Fact]
    public async Task Timestamps_AreUtc_AfterARoundTripThroughTheDatabase()
    {
        var placed = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay()));

        var json = await _anonymous.GetAsync($"{OrdersUrl}/{placed.Reference}")
            .AssertStatus(HttpStatusCode.OK);

        // SQLite doesn't store DateTime.Kind; without the trailing Z a browser would read this as local time.
        Assert.Equal("2026-03-02T10:00:00Z", json.GetProperty("createdAt").GetString());
    }

    // ---- getting by reference ----

    [Fact]
    public async Task Get_ByReferenceAlone_ReturnsTheOrder()
    {
        var placed = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay()));

        var fetched = await _anonymous.GetAsync($"{OrdersUrl}/{placed.Reference}").ReadAs<CustomerOrderResponse>();

        Assert.Equal(placed, fetched);
    }

    [Fact]
    public async Task Get_ReferenceIsCaseInsensitive_AndPrefixOptional()
    {
        var placed = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay()));
        var shortForm = placed.Reference[OrderReference.Prefix.Length..].ToLowerInvariant();

        await _anonymous.GetAsync($"{OrdersUrl}/{shortForm}")
            .AssertStatus(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("BB-23456789")] // valid format, no such order
    [InlineData("not-a-reference")]
    public async Task Get_UnknownReference_Is404(string reference) =>
        await _anonymous.GetAsync($"{OrdersUrl}/{reference}")
            .AssertProblem(HttpStatusCode.NotFound);

    // ---- logged-in customers ----

    [Fact]
    public async Task Place_AsCustomer_IsLinkedToTheAccount()
    {
        var placed = await _customer.PlaceOrder(NewOrder(await _customer.ServiceTypeId(), FreshDay(), email: "other@example.com"));

        await _customer.GetAsync($"{OrdersUrl}/{placed.Reference}").AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AnyOrder_WithItsReference_EvenAsAnotherCustomer()
    {
        var guestOrder = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay()));

        await _customer.GetAsync($"{OrdersUrl}/{guestOrder.Reference}").AssertStatus(HttpStatusCode.OK);
    }
}
