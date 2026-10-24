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
/// The order lifecycle for staff: <c>/api/admin/orders/{id}/status-changes</c> (a sub-collection: each change is
/// a history entry) and <c>/api/admin/orders/{id}/price-override</c> (a singleton sub-resource, Admin only).
/// </summary>
public class AdminOrderLifecycleApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _mechanic = factory.CreateClient(Roles.Mechanic);
    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);

    // ---- status changes ----

    [Fact]
    public async Task ChangeStatus_Returns201_WithLocation_AndWhoChangedIt()
    {
        var id = await PlaceOrder();

        var response = await _mechanic.PostJsonAsync(StatusUrl(id), new ChangeStatusRequest(OrderStatus.InProgress, "Started on it"));

        var change = await response.ReadAs<StatusChangeResponse>(HttpStatusCode.Created);
        Assert.Equal(
            (OrderStatus.New, OrderStatus.InProgress, "Test Mechanic", "Started on it", factory.Clock.GetUtcNow().UtcDateTime),
            (change.From, change.To, change.ChangedBy, change.Reason, change.ChangedAt));
        Assert.EndsWith($"{StatusUrl(id)}/{change.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(change, await _mechanic.GetAsync(response.Headers.Location!.OriginalString).ReadAs<StatusChangeResponse>());
    }

    [Fact]
    public async Task ChangeStatus_UpdatesTheOrder_AndItsAllowedNextStatuses()
    {
        var id = await PlaceOrder();

        await ChangeStatus(id, OrderStatus.InProgress);

        var order = await _mechanic.GetAdminOrder(id);
        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equal([OrderStatus.WaitingForCustomer, OrderStatus.Completed, OrderStatus.Cancelled], order.AllowedStatusChanges);
    }

    [Fact]
    public async Task StatusHistory_ListsChangesOldestFirst()
    {
        var id = await PlaceOrder();
        await ChangeStatus(id, OrderStatus.InProgress);
        await ChangeStatus(id, OrderStatus.WaitingForCustomer, "Need approval for new rim");
        await ChangeStatus(id, OrderStatus.InProgress);
        await ChangeStatus(id, OrderStatus.Completed);

        var history = await _mechanic.GetAsync(StatusUrl(id)).ReadAs<List<StatusChangeResponse>>();

        Assert.Equal(
            [OrderStatus.InProgress, OrderStatus.WaitingForCustomer, OrderStatus.InProgress, OrderStatus.Completed],
            history.Select(c => c.To));
        Assert.Equal("Need approval for new rim", history[1].Reason);
    }

    [Theory]
    [InlineData(OrderStatus.Completed)] // must be in progress first
    [InlineData(OrderStatus.New)] // already New
    public async Task ChangeStatus_InvalidTransition_Is409_AndChangesNothing(OrderStatus to)
    {
        var id = await PlaceOrder();

        await _mechanic.PostJsonAsync(StatusUrl(id), new ChangeStatusRequest(to, null)).AssertProblem(HttpStatusCode.Conflict);

        var order = await _mechanic.GetAdminOrder(id);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Empty(order.StatusHistory);
    }

    [Fact]
    public async Task ChangeStatus_FromClosedOrder_Is409()
    {
        var id = await PlaceOrder();
        await ChangeStatus(id, OrderStatus.Cancelled);

        await _mechanic.PostJsonAsync(StatusUrl(id), new ChangeStatusRequest(OrderStatus.InProgress, null))
            .AssertProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangeStatus_WithoutStatus_Is400_WithFieldError() =>
        (await _mechanic.PostJsonAsync(StatusUrl(await PlaceOrder()), new { reason = "x" }).AssertProblem(HttpStatusCode.BadRequest))
            .AssertFieldError("status");

    [Fact]
    public async Task ChangeStatus_WithUnknownStatus_Is400() =>
        await _mechanic.PostJsonAsync(StatusUrl(await PlaceOrder()), new { status = "Teleported" }).AssertStatus(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Cancelling_FreesCapacity()
    {
        var day = Today.AddDays(100);
        var id = await PlaceOrder(day);

        await ChangeStatus(id, OrderStatus.Cancelled);

        var days = await _anonymous.GetAsync($"/api/capacity?from={day:yyyy-MM-dd}&days=1").ReadAs<List<DayCapacityResponse>>();
        Assert.Equal(0, days[0].Booked);
    }

    [Fact]
    public async Task StatusChanges_OfUnknownOrder_Is404()
    {
        await _mechanic.GetAsync(StatusUrl(Guid.NewGuid())).AssertProblem(HttpStatusCode.NotFound);
        await _mechanic.PostJsonAsync(StatusUrl(Guid.NewGuid()), new ChangeStatusRequest(OrderStatus.InProgress, null))
            .AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StatusChange_Unknown_Is404() =>
        await _mechanic.GetAsync($"{StatusUrl(await PlaceOrder())}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task StatusChanges_AreForStaffOnly()
    {
        var customer = factory.CreateClient(Roles.Customer);

        await customer.PostJsonAsync(StatusUrl(Guid.NewGuid()), new ChangeStatusRequest(OrderStatus.InProgress, null))
            .AssertStatus(HttpStatusCode.Forbidden);
        await _anonymous.GetAsync(StatusUrl(Guid.NewGuid())).AssertStatus(HttpStatusCode.Unauthorized);
    }

    // ---- price override ----

    [Fact]
    public async Task PriceOverride_FirstPut_Is201_ThenReplacing_Is200()
    {
        var id = await PlaceOrder();

        var created = await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(650, "Chain was fine, only cleaned"))
            .ReadAs<PriceOverrideResponse>(HttpStatusCode.Created);
        var replaced = await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(600, "Loyal customer"))
            .ReadAs<PriceOverrideResponse>(HttpStatusCode.OK);

        Assert.Equal((new MoneyResponse(650, "NOK"), "Test Admin"), (created.Amount, created.OverriddenBy));
        Assert.Equal((new MoneyResponse(600, "NOK"), "Loyal customer"), (replaced.Amount, replaced.Reason));
        Assert.Equal(replaced, await _admin.GetAsync(PriceUrl(id)).ReadAs<PriceOverrideResponse>());
    }

    [Fact]
    public async Task PriceOverride_ReplacesTheTotal_ForStaffAndCustomer()
    {
        var (id, reference) = await PlaceOrderWithReference();

        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(500, "Goodwill")).AssertStatus(HttpStatusCode.Created);

        var staffView = await _mechanic.GetAdminOrder(id);
        var customerView = await _anonymous.GetAsync($"{OrdersUrl}/{reference}")
            .ReadAs<CustomerOrderResponse>();
        Assert.Equal((500m, true, 790m), (staffView.Price.Total.Amount, staffView.Price.IsOverridden, staffView.Price.ServicePrice.Amount));
        Assert.Equal((500m, true), (customerView.Price.Total.Amount, customerView.Price.IsOverridden));
        Assert.Equal("Goodwill", staffView.PriceOverride?.Reason);
    }

    [Fact]
    public async Task PriceOverride_Reason_IsShownToTheCustomer()
    {
        var (id, reference) = await PlaceOrderWithReference();

        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(650, "Chain was fine, only cleaned")).AssertStatus(HttpStatusCode.Created);

        var customerView = await _anonymous.GetAsync($"{OrdersUrl}/{reference}").ReadAs<CustomerOrderResponse>();
        Assert.Equal((650m, true, "Chain was fine, only cleaned"), (customerView.Price.Total.Amount, customerView.Price.IsOverridden, customerView.Price.OverrideReason));
    }

    [Fact]
    public async Task PriceOverride_Cleared_RemovesTheReasonForTheCustomer()
    {
        var (id, reference) = await PlaceOrderWithReference();
        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(500, "Goodwill")).AssertStatus(HttpStatusCode.Created);

        await _admin.DeleteAsync(PriceUrl(id)).AssertStatus(HttpStatusCode.NoContent);

        var customerView = await _anonymous.GetAsync($"{OrdersUrl}/{reference}").ReadAs<CustomerOrderResponse>();
        Assert.Equal((false, null as string), (customerView.Price.IsOverridden, customerView.Price.OverrideReason));
    }

    [Fact]
    public async Task PriceOverride_AllowedOnCompletedOrder()
    {
        var id = await PlaceOrder();
        await ChangeStatus(id, OrderStatus.InProgress);
        await ChangeStatus(id, OrderStatus.Completed);

        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(700, "Final price")).AssertStatus(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PriceOverride_OnCancelledOrder_Is409()
    {
        var id = await PlaceOrder();
        await ChangeStatus(id, OrderStatus.Cancelled);

        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(700, "x")).AssertProblem(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("""{ "amount": 500 }""", "reason")]
    [InlineData("""{ "reason": "x" }""", "amount")]
    [InlineData("""{ "amount": -1, "reason": "x" }""", "amount")]
    public async Task PriceOverride_InvalidBody_Is400_WithFieldError(string body, string field)
    {
        var response = await _admin.PutAsync(PriceUrl(await PlaceOrder()), new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        (await response.AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError(field);
    }

    [Fact]
    public async Task PriceOverride_Delete_Is204_AndRestoresCalculatedPrice()
    {
        var id = await PlaceOrder();
        await _admin.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(500, "Goodwill")).AssertStatus(HttpStatusCode.Created);

        await _admin.DeleteAsync(PriceUrl(id)).AssertStatus(HttpStatusCode.NoContent);

        var order = await _mechanic.GetAdminOrder(id);
        Assert.Equal((790m, false), (order.Price.Total.Amount, order.Price.IsOverridden));
        await _admin.GetAsync(PriceUrl(id)).AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PriceOverride_DeleteWhenNoneSet_Is204() =>
        await _admin.DeleteAsync(PriceUrl(await PlaceOrder())).AssertStatus(HttpStatusCode.NoContent);

    [Fact]
    public async Task PriceOverride_GetWhenNoneSet_Is404() =>
        await _mechanic.GetAsync(PriceUrl(await PlaceOrder())).AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task PriceOverride_ChangingIsForAdminsOnly()
    {
        var id = await PlaceOrder();

        await _mechanic.PutJsonAsync(PriceUrl(id), new OverridePriceRequest(500, "x")).AssertStatus(HttpStatusCode.Forbidden);
        await _mechanic.DeleteAsync(PriceUrl(id)).AssertStatus(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PriceOverride_OfUnknownOrder_Is404() =>
        await _admin.PutJsonAsync(PriceUrl(Guid.NewGuid()), new OverridePriceRequest(500, "x")).AssertProblem(HttpStatusCode.NotFound);

    // ---- helpers ----

    private static string StatusUrl(Guid id) => $"{AdminOrdersUrl}/{id}/status-changes";

    private static string PriceUrl(Guid id) => $"{AdminOrdersUrl}/{id}/price-override";

    private Task ChangeStatus(Guid id, OrderStatus to, string? reason = null) =>
        _mechanic.PostJsonAsync(StatusUrl(id), new ChangeStatusRequest(to, reason)).AssertStatus(HttpStatusCode.Created);

    private async Task<Guid> PlaceOrder(DateOnly? dueDate = null) => (await PlaceOrderWithReference(dueDate)).Id;

    private async Task<(Guid Id, string Reference)> PlaceOrderWithReference(DateOnly? dueDate = null)
    {
        var order = await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), dueDate ?? FreshDay()));
        return (await _mechanic.AdminId(order), order.Reference);
    }
}
