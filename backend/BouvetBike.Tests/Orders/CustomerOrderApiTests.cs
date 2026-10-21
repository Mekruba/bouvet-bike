using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.OrderTestHelpers;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// The customer's own order after placing it: <c>PATCH /api/orders/{reference}</c> (JSON Patch, notes only,
/// while the order is New) and <c>GET /api/orders</c> (a logged-in customer's orders).
/// </summary>
public class CustomerOrderApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly (string, string) GuestContact = (OrderAccess.ContactHeader, "kari@example.com");

    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _customer = factory.CreateClient(Roles.Customer);

    // ---- PATCH notes ----

    [Fact]
    public async Task PatchNotes_AsGuest_UpdatesAndReturnsOrder()
    {
        var order = await PlaceGuestOrder(notes: "Squeaks");

        var patched = await _anonymous.PatchJsonAsync(
                Url(order), """[{ "op": "replace", "path": "/notes", "value": "Squeaks when braking" }]""", GuestContact)
            .ReadAs<CustomerOrderResponse>();

        Assert.Equal("Squeaks when braking", patched.Notes);
        Assert.Equal("Squeaks when braking", (await Get(order)).Notes);
    }

    [Fact]
    public async Task PatchNotes_Add_WhenThereAreNone()
    {
        var order = await PlaceGuestOrder(notes: null);

        var patched = await _anonymous.PatchJsonAsync(Url(order), """[{ "op": "add", "path": "/notes", "value": "Back wheel too" }]""", GuestContact)
            .ReadAs<CustomerOrderResponse>();

        Assert.Equal("Back wheel too", patched.Notes);
    }

    [Fact]
    public async Task PatchNotes_Remove_ClearsThem()
    {
        var order = await PlaceGuestOrder(notes: "Squeaks");

        var patched = await _anonymous.PatchJsonAsync(Url(order), """[{ "op": "remove", "path": "/notes" }]""", GuestContact)
            .ReadAs<CustomerOrderResponse>();

        Assert.Null(patched.Notes);
    }

    [Fact]
    public async Task PatchNotes_AsOwningCustomer_NeedsNoContact()
    {
        var order = await _customer.PlaceOrder(NewOrder(await _customer.ServiceTypeId(), FreshDay()));

        await _customer.PatchJsonAsync(Url(order), """[{ "op": "replace", "path": "/notes", "value": "Thanks" }]""")
            .AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchNotes_AfterRepairHasStarted_Is422_AndChangesNothing()
    {
        var order = await PlaceGuestOrder(notes: "Original");
        await factory.SetStatus(order.Reference, OrderStatus.InProgress);

        await _anonymous.PatchJsonAsync(Url(order), """[{ "op": "replace", "path": "/notes", "value": "Too late" }]""", GuestContact)
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

        var current = await Get(order);
        Assert.Equal(("Original", false), (current.Notes, current.CanEditNotes));
    }

    [Theory]
    [InlineData("""[{ "op": "replace", "path": "/bikeBrand", "value": "Giant" }]""")]
    [InlineData("""[{ "op": "replace", "path": "/status", "value": "Completed" }]""")]
    [InlineData("""[{ "op": "replace", "path": "/notes", "value": "ok" }, { "op": "replace", "path": "/dueDate", "value": "2026-04-01" }]""")]
    public async Task Patch_AnythingButNotes_Is422_AndChangesNothing(string patch)
    {
        var order = await PlaceGuestOrder(notes: "Original");

        await _anonymous.PatchJsonAsync(Url(order), patch, GuestContact).AssertProblem(HttpStatusCode.UnprocessableEntity);

        Assert.Equal("Original", (await Get(order)).Notes);
    }

    [Fact]
    public async Task PatchNotes_TooLong_Is422_WithFieldError()
    {
        var order = await PlaceGuestOrder();
        var tooLong = new string('x', MaintenanceOrder.NotesMaxLength + 1);

        var problem = await _anonymous.PatchJsonAsync(Url(order), $$"""[{ "op": "replace", "path": "/notes", "value": "{{tooLong}}" }]""", GuestContact)
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

        problem.AssertFieldError("notes");
    }

    [Fact]
    public async Task PatchNotes_FailingTestOperation_Is409()
    {
        var order = await PlaceGuestOrder(notes: "Original");

        await _anonymous.PatchJsonAsync(Url(order), """
            [
              { "op": "test", "path": "/notes", "value": "Something else" },
              { "op": "replace", "path": "/notes", "value": "Changed" }
            ]
            """, GuestContact).AssertProblem(HttpStatusCode.Conflict);

        Assert.Equal("Original", (await Get(order)).Notes);
    }

    [Fact]
    public async Task PatchNotes_WithoutOrWithWrongContact_Is404()
    {
        var order = await PlaceGuestOrder();
        const string patch = """[{ "op": "replace", "path": "/notes", "value": "Hijacked" }]""";

        await _anonymous.PatchJsonAsync(Url(order), patch).AssertProblem(HttpStatusCode.NotFound);
        await _anonymous.PatchJsonAsync(Url(order), patch, (OrderAccess.ContactHeader, "someone@else.com")).AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchNotes_WithPlainJsonContentType_Is415()
    {
        var order = await PlaceGuestOrder();

        await _anonymous.PatchJsonAsync(Url(order), """[{ "op": "replace", "path": "/notes", "value": "x" }]""", "application/json")
            .AssertStatus(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task PatchNotes_MalformedDocument_Is400()
    {
        var order = await PlaceGuestOrder();

        await _anonymous.PatchJsonAsync(Url(order), """[{ "op": "frobnicate", "path": "/notes" }]""", GuestContact)
            .AssertStatus(HttpStatusCode.BadRequest);
    }

    // ---- GET my orders ----

    [Fact]
    public async Task MyOrders_ListsOnlyTheCustomersOwnOrders_NewestFirst()
    {
        var serviceId = await _customer.ServiceTypeId();
        var older = await _customer.PlaceOrder(NewOrder(serviceId, FreshDay()));
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await _customer.PlaceOrder(NewOrder(serviceId, FreshDay()));
        var guestOrder = await PlaceGuestOrder(); // same email, but not linked to the account

        var mine = await _customer.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();

        var references = mine.Select(o => o.Reference).ToList();
        Assert.True(references.IndexOf(newer.Reference) < references.IndexOf(older.Reference), "Newest first");
        Assert.DoesNotContain(guestOrder.Reference, references);
        var summary = Assert.Single(mine, o => o.Reference == newer.Reference);
        Assert.Equal((OrderStatus.New, "Trek", "Chain replacement", newer.DueDate, newer.Price.Total), (summary.Status, summary.BikeBrand, summary.ServiceName, summary.DueDate, summary.Total));
    }

    [Fact]
    public async Task MyOrders_RequiresLogin() =>
        await _anonymous.GetAsync(OrdersUrl).AssertStatus(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Mechanic)]
    public async Task MyOrders_IsForCustomers_StaffUseTheAdminApi(string role) =>
        await factory.CreateClient(role).GetAsync(OrdersUrl).AssertStatus(HttpStatusCode.Forbidden);

    // ---- helpers ----

    private static string Url(CustomerOrderResponse order) => $"{OrdersUrl}/{order.Reference}";

    private async Task<CustomerOrderResponse> PlaceGuestOrder(string? notes = null) =>
        await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay(), notes: notes));

    private Task<CustomerOrderResponse> Get(CustomerOrderResponse order) =>
        _anonymous.GetAsync(Url(order), GuestContact).ReadAs<CustomerOrderResponse>();
}
