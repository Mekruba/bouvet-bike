using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;

namespace BouvetBike.Tests.Users;

/// <summary>
/// Real cookie sign-in, end to end (these clients send no test role header):
/// <c>POST /api/users</c> (register), <c>GET /api/users/me</c>, <c>POST</c>/<c>DELETE /api/session</c> (log in/out).
/// Each test uses its own client, so its own cookie jar.
/// </summary>
public class AccountApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string UsersUrl = "/api/users";
    private const string MeUrl = "/api/users/me";
    private const string SessionUrl = "/api/session";
    private const string Password = "Sykkel-2026";

    // ---- register ----

    [Fact]
    public async Task Register_Returns201_AsCustomer_AndSignsIn()
    {
        var client = factory.CreateClient(role: null);
        var email = UniqueEmail();

        var response = await client.PostJsonAsync(UsersUrl, new RegisterRequest("Kari Nordmann", email, "912 34 567", Password));

        var user = await response.ReadAs<CurrentUserResponse>(HttpStatusCode.Created);
        Assert.Equal(("Kari Nordmann", email, "+4791234567", false), (user.Name, user.Email, user.Phone, user.IsStaff));
        Assert.Equal([Roles.Customer], user.Roles);
        Assert.EndsWith(MeUrl, response.Headers.Location?.OriginalString);
        ApiAssert.AssertSameJson(user, await client.GetAsync(MeUrl).ReadAs<CurrentUserResponse>());
    }

    [Fact]
    public async Task Register_SetsASecureCookie()
    {
        var response = await factory.CreateClient(role: null).PostJsonAsync(UsersUrl, new RegisterRequest("Kari", UniqueEmail(), null, Password));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bouvetbike.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithTakenEmail_Is409_IgnoringCase()
    {
        var email = UniqueEmail();
        await Register(factory.CreateClient(role: null), email);

        await factory.CreateClient(role: null).PostJsonAsync(UsersUrl, new RegisterRequest("Other", email.ToUpperInvariant(), null, Password))
            .AssertProblem(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short1!")] // too short: caught by request validation
    [InlineData("alllowercase-no-digits")] // long enough, but fails Identity's rules
    public async Task Register_WithWeakPassword_Is400_WithPasswordError(string password) =>
        (await factory.CreateClient(role: null).PostJsonAsync(UsersUrl, new RegisterRequest("Kari", UniqueEmail(), null, password))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("password");

    [Fact]
    public async Task Register_WithInvalidPhone_Is400_WithPhoneError() =>
        (await factory.CreateClient(role: null).PostJsonAsync(UsersUrl, new RegisterRequest("Kari", UniqueEmail(), "123", Password))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("phone");

    [Fact]
    public async Task Register_WithMissingFields_Is400() =>
        (await factory.CreateClient(role: null).PostJsonAsync(UsersUrl, new { email = UniqueEmail() })
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("password");

    // ---- session ----

    [Fact]
    public async Task Login_Returns200_WithUser_AndCookieWorks()
    {
        var email = UniqueEmail();
        await Register(factory.CreateClient(role: null), email);
        var client = factory.CreateClient(role: null);

        var user = await client.PostJsonAsync(SessionUrl, new LoginRequest(email, Password)).ReadAs<CurrentUserResponse>();

        Assert.Equal(email, user.Email);
        ApiAssert.AssertSameJson(user, await client.GetAsync(MeUrl).ReadAs<CurrentUserResponse>());
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var email = UniqueEmail();
        await Register(factory.CreateClient(role: null), email);

        await factory.CreateClient(role: null).PostJsonAsync(SessionUrl, new LoginRequest(email.ToUpperInvariant(), Password))
            .AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownEmail_AreTheSame401()
    {
        var email = UniqueEmail();
        await Register(factory.CreateClient(role: null), email);
        var client = factory.CreateClient(role: null);

        var wrongPassword = await client.PostJsonAsync(SessionUrl, new LoginRequest(email, "Wrong-Password-1"));
        var unknownEmail = await client.PostJsonAsync(SessionUrl, new LoginRequest(UniqueEmail(), Password));

        var first = await wrongPassword.AssertProblem(HttpStatusCode.Unauthorized);
        var second = await unknownEmail.AssertProblem(HttpStatusCode.Unauthorized);
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
        Assert.NotEmpty(wrongPassword.Headers.WwwAuthenticate); // RFC 9110: a 401 must carry a challenge
        await client.GetAsync(MeUrl).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_LocksOutAfterFiveFailures()
    {
        var email = UniqueEmail();
        await Register(factory.CreateClient(role: null), email);
        var client = factory.CreateClient(role: null);

        for (var i = 0; i < 5; i++)
        {
            await client.PostJsonAsync(SessionUrl, new LoginRequest(email, "Wrong-Password-1")).AssertStatus(HttpStatusCode.Unauthorized);
        }

        await client.PostJsonAsync(SessionUrl, new LoginRequest(email, Password)).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Returns204_AndEndsTheSession()
    {
        var client = factory.CreateClient(role: null);
        await Register(client, UniqueEmail());

        await client.DeleteAsync(SessionUrl).AssertStatus(HttpStatusCode.NoContent);

        await client.GetAsync(MeUrl).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WhenAnonymous_Is401_WithChallenge()
    {
        var response = await factory.CreateClient(role: null).GetAsync(MeUrl);

        await response.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.NotEmpty(response.Headers.WwwAuthenticate);
    }

    // ---- end to end ----

    [Fact]
    public async Task RegisteredCustomer_OrdersAreLinkedToTheAccount()
    {
        var client = factory.CreateClient(role: null);
        await Register(client, UniqueEmail());

        var order = await client.PlaceOrder(NewOrder(await client.ServiceTypeId(), FreshDay()));

        var mine = await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Equal(order.Reference, Assert.Single(mine).Reference);
    }

    [Fact]
    public async Task SeededAdmin_CanLogIn_AndUseTheAdminApi()
    {
        var client = factory.CreateClient(role: null);

        var admin = await client.PostJsonAsync(SessionUrl, new LoginRequest(ApiFactory.SeededAdminEmail, ApiFactory.SeededAdminPassword))
            .ReadAs<CurrentUserResponse>();

        Assert.Equal([Roles.Admin], admin.Roles);
        Assert.True(admin.IsStaff);
        await client.GetAsync("/api/admin/orders").AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_IsForbiddenFromTheAdminApi()
    {
        var client = factory.CreateClient(role: null);
        await Register(client, UniqueEmail());

        await client.GetAsync("/api/admin/orders").AssertStatus(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private static Task Register(HttpClient client, string email) =>
        client.PostJsonAsync(UsersUrl, new RegisterRequest("Kari Nordmann", email, null, Password)).AssertStatus(HttpStatusCode.Created);
}
