using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Tests.Orders;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.Users.AccountTestHelpers;

namespace BouvetBike.Tests.Shared;

/// <summary>
/// Responses with personal data are sent with <c>Cache-Control: no-store</c> (RFC 9111), so no browser or shared cache keeps them.
/// <c>GET /api/orders</c> depends on the <c>Order-Contact</c> header or the login cookie, so a cached copy could reach the wrong person.
/// </summary>
public class CacheControlApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _staff = factory.CreateClient(Roles.Mechanic);

    private async Task<CustomerOrderResponse> PlaceOrder(string email) =>
        await _anonymous.PlaceOrder(NewOrder(await _anonymous.ServiceTypeId(), FreshDay(), email: email));

    [Fact]
    public async Task CustomerOrders_AreNotStored()
    {
        var email = UniqueEmail();
        var placed = await _anonymous.PostJsonAsync(OrdersUrl, NewOrder(await _anonymous.ServiceTypeId(), FreshDay(), email: email));
        var order = await placed.ReadAs<CustomerOrderResponse>(HttpStatusCode.Created);

        using var byContact = new HttpRequestMessage(HttpMethod.Get, OrdersUrl);
        byContact.Headers.Add("Order-Contact", email);

        AssertNoStore(placed);
        AssertNoStore(await Ok(_anonymous.SendAsync(byContact)));
        AssertNoStore(await Ok(_anonymous.GetAsync($"{OrdersUrl}/{order.Reference}")));
        AssertNoStore(await Ok(_anonymous.PatchJsonAsync($"{OrdersUrl}/{order.Reference}", """[{ "op": "replace", "path": "/notes", "value": "Squeaky brakes" }]""")));
    }

    [Fact]
    public async Task MyOrders_AreNotStored()
    {
        var customer = await factory.SignedInCustomer(UniqueEmail());

        AssertNoStore(await Ok(customer.GetAsync(OrdersUrl)));
    }

    [Fact]
    public async Task StaffOrderViews_AreNotStored()
    {
        var order = await PlaceOrder(UniqueEmail());
        var id = await _staff.AdminId(order);

        AssertNoStore(await Ok(_staff.GetAsync(AdminOrderTestHelpers.AdminOrdersUrl)));
        AssertNoStore(await Ok(_staff.GetAsync($"{AdminOrderTestHelpers.AdminOrdersUrl}/{id}")));
        AssertNoStore(await Ok(_staff.GetAsync($"{AdminOrderTestHelpers.AdminOrdersUrl}/{id}/status-changes")));
    }

    [Fact]
    public async Task Accounts_AreNotStored()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient(role: null);
        var registered = await client.Register(email);
        var registration = await registered.ReadAs<RegistrationResponse>(HttpStatusCode.Created);
        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);

        AssertNoStore(registered);
        AssertNoStore(await Ok(client.Login(email)));
        AssertNoStore(await Ok(client.GetAsync("/api/users/me")));
        AssertNoStore(await Ok(factory.CreateClient(Roles.Admin).GetAsync("/api/admin/staff")));
    }

    private static async Task<HttpResponseMessage> Ok(Task<HttpResponseMessage> request)
    {
        var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.True(response.Headers.CacheControl?.NoStore, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: expected Cache-Control: no-store");
}
