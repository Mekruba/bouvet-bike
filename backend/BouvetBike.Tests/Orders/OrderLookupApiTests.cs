using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using static BouvetBike.Tests.Orders.OrderTestHelpers;

namespace BouvetBike.Tests.Orders;

/// <summary>
/// Guests find their orders with only an email or a phone number: <c>GET /api/orders</c> with the
/// <c>Order-Contact</c> header (kept out of the URL). Lists every order with that contact, any status, newest first.
/// </summary>
public class OrderLookupApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);

    [Fact]
    public async Task ByEmail_ListsTheWholeHistory_NewestFirst_IgnoringCase()
    {
        var email = UniqueEmail();
        var first = await Place(email: email);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await Place(email: email);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var third = await Place(email: email);
        await factory.SetStatus(first.Reference, OrderStatus.InProgress);
        await factory.SetStatus(first.Reference, OrderStatus.Completed);
        await factory.CancelOrder(second.Reference);

        var orders = await Find(email.ToUpperInvariant());

        Assert.Equal([third.Reference, second.Reference, first.Reference], orders.Select(o => o.Reference));
        Assert.Equal([OrderStatus.New, OrderStatus.Cancelled, OrderStatus.Completed], orders.Select(o => o.Status));
    }

    [Fact]
    public async Task ByPhone_InAnyFormat()
    {
        var digits = UniquePhoneDigits();
        var order = await Place(phone: $"{digits[..3]} {digits[3..5]} {digits[5..]}");

        var orders = await Find($"+47 {digits}");

        Assert.Equal(order.Reference, Assert.Single(orders).Reference);
    }

    [Fact]
    public async Task OnlyListsOrdersWithThatContact()
    {
        var email = UniqueEmail();
        var mine = await Place(email: email);
        await Place(email: UniqueEmail());

        var orders = await Find(email);

        Assert.Equal(mine.Reference, Assert.Single(orders).Reference);
    }

    [Fact]
    public async Task NoMatches_IsAnEmptyList() =>
        Assert.Empty(await Find(UniqueEmail()));

    [Fact]
    public async Task NeitherEmailNorPhone_Is400() =>
        await _anonymous.GetAsync(OrdersUrl, (OrderAccess.ContactHeader, "not an email or phone")).AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task ResultsLinkToTheOrder_ByReferenceAlone()
    {
        var email = UniqueEmail();
        await Place(email: email);
        var summary = Assert.Single(await Find(email));

        var order = await _anonymous.GetAsync($"{OrdersUrl}/{summary.Reference}").ReadAs<CustomerOrderResponse>();

        Assert.Equal(email, order.Contact.Email);
    }

    private Task<List<CustomerOrderSummaryResponse>> Find(string contact) =>
        _anonymous.GetAsync(OrdersUrl, (OrderAccess.ContactHeader, contact)).ReadAs<List<CustomerOrderSummaryResponse>>();

    private async Task<CustomerOrderResponse> Place(string? email = null, string phone = "912 34 567") =>
        await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay(), email: email ?? UniqueEmail(), phone: phone));

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    /// <summary>8 digits starting with 4 (a Norwegian mobile number), unique enough for a test run.</summary>
    private static string UniquePhoneDigits() => "4" + Random.Shared.Next(0, 10_000_000).ToString("D7");
}
