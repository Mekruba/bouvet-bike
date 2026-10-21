using System.Net;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.AdminOrderTestHelpers;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// Staff edits: <c>PATCH /api/admin/orders/{id}</c> (JSON Patch on <see cref="AdminOrderPatch"/>) and
/// <c>DELETE /api/admin/orders/{id}</c> (Admin only).
/// </summary>
public class AdminOrderUpdateApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _mechanic = factory.CreateClient(Roles.Mechanic);
    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);

    // ---- PATCH details ----

    [Fact]
    public async Task Patch_Details_UpdatesAndNormalizesContact()
    {
        var id = await PlaceOrder();

        var order = await Patch(id, """
            [
              { "op": "replace", "path": "/name", "value": "Ola Nordmann" },
              { "op": "replace", "path": "/email", "value": "Ola@Example.com" },
              { "op": "replace", "path": "/phone", "value": "+47 400 00 000" },
              { "op": "replace", "path": "/bikeBrand", "value": "Giant" },
              { "op": "remove", "path": "/bikeModel" }
            ]
            """).ReadAs<AdminOrderResponse>();

        Assert.Equal(new ContactResponse("Ola Nordmann", "ola@example.com", "+4740000000"), order.Contact);
        Assert.Equal(("Giant", null as string), (order.BikeBrand, order.BikeModel));
        ApiAssert.AssertSameJson(order, await _mechanic.GetAdminOrder(id));
    }

    [Fact]
    public async Task Patch_InternalNotes_AllowedEvenOnCompletedOrder()
    {
        var (id, reference) = await PlaceOrderWithReference();
        await factory.SetStatus(reference, OrderStatus.InProgress);
        await factory.SetStatus(reference, OrderStatus.Completed);

        var order = await Patch(id, """[{ "op": "add", "path": "/internalNotes", "value": "Picked up Friday" }]""").ReadAs<AdminOrderResponse>();

        Assert.Equal("Picked up Friday", order.InternalNotes);
    }

    [Fact]
    public async Task Patch_Details_OnCompletedOrder_Is409()
    {
        var (id, reference) = await PlaceOrderWithReference();
        await factory.SetStatus(reference, OrderStatus.InProgress);
        await factory.SetStatus(reference, OrderStatus.Completed);

        await Patch(id, """[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""").AssertProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Patch_InternalNotes_AreNeverShownToTheCustomer()
    {
        var (id, reference) = await PlaceOrderWithReference();
        await Patch(id, """[{ "op": "add", "path": "/internalNotes", "value": "Secret" }]""").AssertStatus(HttpStatusCode.OK);

        var json = await _anonymous.GetAsync($"{OrdersUrl}/{reference}")
            .AssertStatus(HttpStatusCode.OK);

        Assert.DoesNotContain("Secret", json.GetRawText());
    }

    // ---- PATCH service and due date ----

    [Fact]
    public async Task Patch_ServiceType_RepricesFromCatalog()
    {
        var id = await PlaceOrder();
        var brakes = await _anonymous.ServiceTypeId("Brake maintenance");

        var order = await Patch(id, $$"""[{ "op": "replace", "path": "/serviceTypeId", "value": "{{brakes}}" }]""").ReadAs<AdminOrderResponse>();

        Assert.Equal(("Brake maintenance", new MoneyResponse(690, "NOK")), (order.ServiceName, order.Price.Total));
    }

    [Fact]
    public async Task Patch_UnknownServiceType_Is422() =>
        await Patch(await PlaceOrder(), $$"""[{ "op": "replace", "path": "/serviceTypeId", "value": "{{Guid.NewGuid()}}" }]""")
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

    [Fact]
    public async Task Patch_DueDate_Moves()
    {
        var id = await PlaceOrder();
        var newDate = FreshDay();

        var order = await Patch(id, $$"""[{ "op": "replace", "path": "/dueDate", "value": "{{newDate:yyyy-MM-dd}}" }]""").ReadAs<AdminOrderResponse>();

        Assert.Equal(newDate, order.DueDate);
    }

    [Fact]
    public async Task Patch_DueDateToFullDay_Is422_WithNextAvailableDate()
    {
        var full = Today.AddDays(70);
        await _anonymous.PlaceOrders(await _anonymous.ServiceTypeId(), full, count: 5);
        var id = await PlaceOrder();

        var problem = await Patch(id, $$"""[{ "op": "replace", "path": "/dueDate", "value": "{{full:yyyy-MM-dd}}" }]""")
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

        Assert.Equal($"{full.AddDays(1):yyyy-MM-dd}", problem.GetProperty("nextAvailableDate").GetString());
    }

    [Fact]
    public async Task Patch_OtherFieldsOfAnOrderOnAFullDay_IsAllowed()
    {
        var full = Today.AddDays(80);
        await _anonymous.PlaceOrders(await _anonymous.ServiceTypeId(), full, count: 4);
        var id = await PlaceOrder(dueDate: full); // the fifth: the day is now full, including this order

        await Patch(id, """[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""").AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_DueDateInThePast_Is422() =>
        await Patch(await PlaceOrder(), $$"""[{ "op": "replace", "path": "/dueDate", "value": "{{Today.AddDays(-1):yyyy-MM-dd}}" }]""")
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

    // ---- PATCH discount code ----

    [Fact]
    public async Task Patch_DiscountCode_AppliesAndRemoves()
    {
        await factory.SeedDiscountCode("STAFF50", DiscountType.Percentage, 50);
        var id = await PlaceOrder();

        var discounted = await Patch(id, """[{ "op": "add", "path": "/discountCode", "value": "staff50" }]""").ReadAs<AdminOrderResponse>();
        var removed = await Patch(id, """[{ "op": "remove", "path": "/discountCode" }]""").ReadAs<AdminOrderResponse>();

        Assert.Equal(("STAFF50", 395m), (discounted.Price.DiscountCode, discounted.Price.Total.Amount));
        Assert.Equal((null as string, 790m), (removed.Price.DiscountCode, removed.Price.Total.Amount));
    }

    [Fact]
    public async Task Patch_DiscountCode_ReplacesExistingCode()
    {
        await factory.SeedDiscountCode("FIRST10", DiscountType.Percentage, 10);
        await factory.SeedDiscountCode("SECOND20", DiscountType.Percentage, 20);
        var id = await PlaceOrder(discountCode: "FIRST10");

        var order = await Patch(id, """[{ "op": "replace", "path": "/discountCode", "value": "SECOND20" }]""").ReadAs<AdminOrderResponse>();

        Assert.Equal(("SECOND20", 632m), (order.Price.DiscountCode, order.Price.Total.Amount));
    }

    [Fact]
    public async Task Patch_InvalidDiscountCode_Is422() =>
        await Patch(await PlaceOrder(), """[{ "op": "add", "path": "/discountCode", "value": "NOPE" }]""")
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

    // ---- PATCH protocol ----

    [Theory]
    [InlineData("""[{ "op": "replace", "path": "/status", "value": "Completed" }]""")] // has its own resource
    [InlineData("""[{ "op": "replace", "path": "/id", "value": "00000000-0000-0000-0000-000000000001" }]""")]
    [InlineData("""[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }, { "op": "remove", "path": "/name" }]""")]
    public async Task Patch_ReadOnlyUnknownOrRequiredPath_Is422_AndChangesNothing(string patch)
    {
        var id = await PlaceOrder();
        var before = await _mechanic.GetAdminOrder(id);

        await Patch(id, patch).AssertProblem(HttpStatusCode.UnprocessableEntity);

        ApiAssert.AssertSameJson(before, await _mechanic.GetAdminOrder(id));
    }

    [Fact]
    public async Task Patch_FailingTestOperation_Is409()
    {
        var id = await PlaceOrder();

        await Patch(id, """[{ "op": "test", "path": "/bikeBrand", "value": "Giant" }, { "op": "replace", "path": "/bikeBrand", "value": "Cube" }]""")
            .AssertProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Patch_WithPlainJsonContentType_Is415() =>
        await _mechanic.PatchJsonAsync($"{AdminOrdersUrl}/{await PlaceOrder()}", """[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""", "application/json")
            .AssertStatus(HttpStatusCode.UnsupportedMediaType);

    [Fact]
    public async Task Patch_MalformedDocument_Is400() =>
        await Patch(await PlaceOrder(), """[{ "op": "frobnicate", "path": "/bikeBrand" }]""").AssertStatus(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Patch_Unknown_Is404() =>
        await Patch(Guid.NewGuid(), """[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""").AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task Patch_IsForStaffOnly() =>
        await factory.CreateClient(Roles.Customer).PatchJsonAsync($"{AdminOrdersUrl}/{Guid.NewGuid()}", """[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""")
            .AssertStatus(HttpStatusCode.Forbidden);

    // ---- DELETE ----

    [Fact]
    public async Task Delete_AsAdmin_Is204_AndFreesCapacity()
    {
        var day = Today.AddDays(90);
        var id = await PlaceOrder(dueDate: day);

        await _admin.DeleteAsync($"{AdminOrdersUrl}/{id}").AssertStatus(HttpStatusCode.NoContent);

        await _admin.GetAsync($"{AdminOrdersUrl}/{id}").AssertProblem(HttpStatusCode.NotFound);
        var days = await _anonymous.GetAsync($"/api/capacity?from={day:yyyy-MM-dd}&days=1").ReadAs<List<DayCapacityResponse>>();
        Assert.Equal(0, days[0].Booked);
    }

    [Fact]
    public async Task Delete_OrderWithHistory_Is204()
    {
        var (id, reference) = await PlaceOrderWithReference();
        await factory.SetStatus(reference, OrderStatus.Cancelled);

        await _admin.DeleteAsync($"{AdminOrdersUrl}/{id}").AssertStatus(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_IsForAdminsOnly() =>
        await _mechanic.DeleteAsync($"{AdminOrdersUrl}/{await PlaceOrder()}").AssertStatus(HttpStatusCode.Forbidden);

    [Fact]
    public async Task Delete_Unknown_Is404() =>
        await _admin.DeleteAsync($"{AdminOrdersUrl}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    // ---- helpers ----

    private Task<HttpResponseMessage> Patch(Guid id, string patch) => _mechanic.PatchJsonAsync($"{AdminOrdersUrl}/{id}", patch);

    private async Task<Guid> PlaceOrder(DateOnly? dueDate = null, string? discountCode = null) =>
        (await PlaceOrderWithReference(dueDate, discountCode)).Id;

    private async Task<(Guid Id, string Reference)> PlaceOrderWithReference(DateOnly? dueDate = null, string? discountCode = null)
    {
        var order = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), dueDate ?? FreshDay(), discountCode: discountCode));
        return (await _mechanic.AdminId(order), order.Reference);
    }
}
