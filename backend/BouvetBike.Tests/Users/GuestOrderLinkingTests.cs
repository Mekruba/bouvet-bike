using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;

namespace BouvetBike.Tests.Users;

/// <summary>
/// Registering links the guest orders booked with the same email to the new account, so they show up under
/// "My orders". Orders already on another account stay there.
/// </summary>
public class GuestOrderLinkingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Sykkel-2026";

    [Fact]
    public async Task Register_LinksGuestOrdersWithTheSameEmail_IgnoringCase()
    {
        var email = UniqueEmail();
        var guest = factory.CreateClient(role: null);
        var first = await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: email));
        var second = await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: email.ToUpperInvariant()));
        await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: UniqueEmail()));

        var client = factory.CreateClient(role: null);
        await client.PostJsonAsync("/api/users", new RegisterRequest("Kari Nordmann", email, null, Password)).AssertStatus(HttpStatusCode.Created);

        var mine = await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Equal(new[] { first.Reference, second.Reference }.Order(), mine.Select(o => o.Reference).Order());
    }

    [Fact]
    public async Task Register_DoesNotMatchOnPhone()
    {
        var guest = factory.CreateClient(role: null);
        await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: UniqueEmail(), phone: "+47 400 11 223"));

        var client = factory.CreateClient(role: null);
        await client.PostJsonAsync("/api/users", new RegisterRequest("Kari", UniqueEmail(), "+47 400 11 223", Password)).AssertStatus(HttpStatusCode.Created);

        Assert.Empty(await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>());
    }

    [Fact]
    public async Task Register_LeavesOrdersOnAnotherAccountAlone()
    {
        var email = UniqueEmail();
        var existingCustomer = factory.CreateClient(Roles.Customer);
        var theirs = await existingCustomer.PlaceOrder(NewOrder(await existingCustomer.ServiceTypeId(), FreshDay(), email: email));

        var client = factory.CreateClient(role: null);
        await client.PostJsonAsync("/api/users", new RegisterRequest("Someone", email, null, Password)).AssertStatus(HttpStatusCode.Created);

        Assert.Empty(await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>());
        var stillTheirs = await existingCustomer.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Contains(stillTheirs, o => o.Reference == theirs.Reference);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
}
