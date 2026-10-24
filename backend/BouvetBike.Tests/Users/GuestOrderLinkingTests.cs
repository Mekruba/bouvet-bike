using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Tests.Orders;
using BouvetBike.Users;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.Users.AccountTestHelpers;

namespace BouvetBike.Tests.Users;

/// <summary>
/// Confirming the email links the guest orders booked with it to the account, so they show up under
/// "My orders". Only a confirmed email claims orders; orders already on another account stay there.
/// </summary>
public class GuestOrderLinkingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ConfirmingTheEmail_LinksGuestOrdersWithIt_IgnoringCase()
    {
        var email = UniqueEmail();
        var guest = factory.CreateClient(role: null);
        var first = await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: email));
        var second = await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: email.ToUpperInvariant()));
        await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: UniqueEmail()));

        var client = await factory.SignedInCustomer(email);

        var mine = await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Equal(new[] { first.Reference, second.Reference }.Order(), mine.Select(o => o.Reference).Order());
    }

    [Fact]
    public async Task RegisteringWithoutConfirming_DoesNotLinkAnything()
    {
        var email = UniqueEmail();
        var guest = factory.CreateClient(role: null);
        var order = await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: email));

        await factory.CreateClient(role: null).RegisterOk(email);

        var staff = factory.CreateClient(Roles.Mechanic);
        Assert.True((await staff.Search($"search={order.Reference}")).Items.Single().IsGuest);
    }

    [Fact]
    public async Task DoesNotMatchOnPhone()
    {
        var guest = factory.CreateClient(role: null);
        await guest.PlaceOrder(NewOrder(await guest.ServiceTypeId(), FreshDay(), email: UniqueEmail(), phone: "+47 400 11 223"));

        var client = factory.CreateClient(role: null);
        var registration = await client.RegisterOk(UniqueEmail(), phone: "+47 400 11 223");
        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);
        await client.Login(registration.User.Email).AssertStatus(HttpStatusCode.OK);

        Assert.Empty(await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>());
    }

    [Fact]
    public async Task LeavesOrdersOnAnotherAccountAlone()
    {
        var email = UniqueEmail();
        var existingCustomer = factory.CreateClient(Roles.Customer);
        var theirs = await existingCustomer.PlaceOrder(NewOrder(await existingCustomer.ServiceTypeId(), FreshDay(), email: email));

        var client = await factory.SignedInCustomer(email);

        Assert.Empty(await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>());
        var stillTheirs = await existingCustomer.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Contains(stillTheirs, o => o.Reference == theirs.Reference);
    }
}
