using System.Net;
using BouvetBike.Orders.Contracts;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;
using static BouvetBike.Tests.Orders.OrderTestHelpers;
using static BouvetBike.Tests.Users.AccountTestHelpers;

namespace BouvetBike.Tests.Users;

/// <summary>
/// Real cookie sign-in, end to end (these clients send no test role header): register (<c>POST /api/users</c>),
/// confirm the email (<c>POST /api/users/{id}/email-confirmation</c>), log in and out (<c>/api/session</c>).
/// Each test uses its own client, so its own cookie jar.
/// </summary>
public class AccountApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MeUrl = "/api/users/me";
    private const string SessionUrl = "/api/session";

    // ---- register ----

    [Fact]
    public async Task Register_Returns201_AsCustomer_WithConfirmationLink_WithoutSigningIn()
    {
        var client = factory.CreateClient(role: null);
        var email = UniqueEmail();

        var response = await client.Register(email, phone: "912 34 567");

        var registration = await response.ReadAs<RegistrationResponse>(HttpStatusCode.Created);
        Assert.Equal(("Kari Nordmann", email, "+4791234567", false), (registration.User.Name, registration.User.Email, registration.User.Phone, registration.User.IsStaff));
        Assert.Equal([Roles.Customer], registration.User.Roles);
        Assert.StartsWith("/confirm-email?userId=", registration.EmailConfirmationLink);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        await client.GetAsync(MeUrl).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithTakenEmail_Is409_IgnoringCase()
    {
        var email = UniqueEmail();
        await factory.CreateClient(role: null).RegisterOk(email);

        await factory.CreateClient(role: null).Register(email.ToUpperInvariant()).AssertProblem(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short1!")] // too short: caught by request validation
    [InlineData("alllowercase-no-digits")] // long enough, but fails Identity's rules
    public async Task Register_WithWeakPassword_Is400_WithPasswordError(string password) =>
        (await factory.CreateClient(role: null).PostJsonAsync("/api/users", new RegisterRequest("Kari", UniqueEmail(), null, password))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("password");

    [Fact]
    public async Task Register_WithInvalidPhone_Is400_WithPhoneError() =>
        (await factory.CreateClient(role: null).Register(UniqueEmail(), phone: "123").AssertProblem(HttpStatusCode.BadRequest))
            .AssertFieldError("phone");

    [Fact]
    public async Task Register_WithMissingFields_Is400() =>
        (await factory.CreateClient(role: null).PostJsonAsync("/api/users", new { email = UniqueEmail() })
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("password");

    // ---- email confirmation ----

    [Fact]
    public async Task Login_BeforeConfirmingTheEmail_Is403()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient(role: null);
        await client.RegisterOk(email);

        var problem = await client.Login(email).AssertProblem(HttpStatusCode.Forbidden);

        Assert.Contains("confirm", problem.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        await client.GetAsync(MeUrl).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_BeforeConfirming_WithWrongPassword_IsStill401()
    {
        var email = UniqueEmail();
        await factory.CreateClient(role: null).RegisterOk(email);

        await factory.CreateClient(role: null).Login(email, "Wrong-Password-1").AssertProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfirmationLink_Is204_ThenLoginWorks()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient(role: null);
        var registration = await client.RegisterOk(email);

        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);

        var user = await client.Login(email).ReadAs<CurrentUserResponse>();
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task ConfirmationLink_IsIdempotent()
    {
        var client = factory.CreateClient(role: null);
        var registration = await client.RegisterOk(UniqueEmail());

        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);
        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ConfirmationLink_WithWrongToken_Is400()
    {
        var client = factory.CreateClient(role: null);
        var registration = await client.RegisterOk(UniqueEmail());

        await client.PostJsonAsync($"/api/users/{registration.User.Id}/email-confirmation", new ConfirmEmailRequest("not-the-token"))
            .AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmationLink_ForUnknownUser_Is400() =>
        await factory.CreateClient(role: null).PostJsonAsync($"/api/users/{Guid.NewGuid()}/email-confirmation", new ConfirmEmailRequest("x"))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task NewConfirmationLink_CanBeRequested_AndWorks()
    {
        var email = UniqueEmail();
        var client = factory.CreateClient(role: null);
        await client.RegisterOk(email);

        var resent = await client.PostJsonAsync("/api/email-confirmation-requests", new EmailConfirmationRequest(email.ToUpperInvariant()))
            .ReadAs<EmailConfirmationRequestResponse>(HttpStatusCode.Accepted);

        await client.FollowConfirmationLink(resent.EmailConfirmationLink!).AssertStatus(HttpStatusCode.NoContent);
        await client.Login(email).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NewConfirmationLink_ForUnknownOrConfirmedEmail_Is202_WithoutALink()
    {
        var confirmed = UniqueEmail();
        await factory.SignedInCustomer(confirmed);
        var client = factory.CreateClient(role: null);

        var unknown = await client.PostJsonAsync("/api/email-confirmation-requests", new EmailConfirmationRequest(UniqueEmail()))
            .ReadAs<EmailConfirmationRequestResponse>(HttpStatusCode.Accepted);
        var alreadyConfirmed = await client.PostJsonAsync("/api/email-confirmation-requests", new EmailConfirmationRequest(confirmed))
            .ReadAs<EmailConfirmationRequestResponse>(HttpStatusCode.Accepted);

        Assert.Null(unknown.EmailConfirmationLink);
        Assert.Null(alreadyConfirmed.EmailConfirmationLink);
    }

    // ---- session ----

    [Fact]
    public async Task Login_Returns200_WithUser_AndASecureCookie()
    {
        var email = UniqueEmail();
        var client = await factory.SignedInCustomer(email);
        var fresh = factory.CreateClient(role: null);

        var response = await fresh.Login(email);

        var user = await response.ReadAs<CurrentUserResponse>();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bouvetbike.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        ApiAssert.AssertSameJson(user, await fresh.GetAsync(MeUrl).ReadAs<CurrentUserResponse>());
        await client.GetAsync(MeUrl).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var email = UniqueEmail();
        await factory.SignedInCustomer(email);

        await factory.CreateClient(role: null).Login(email.ToUpperInvariant()).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownEmail_AreTheSame401()
    {
        var email = UniqueEmail();
        await factory.SignedInCustomer(email);
        var client = factory.CreateClient(role: null);

        var wrongPassword = await client.Login(email, "Wrong-Password-1");
        var unknownEmail = await client.Login(UniqueEmail());

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
        await factory.SignedInCustomer(email);
        var client = factory.CreateClient(role: null);

        for (var i = 0; i < 5; i++)
        {
            await client.Login(email, "Wrong-Password-1").AssertStatus(HttpStatusCode.Unauthorized);
        }

        await client.Login(email).AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Returns204_AndEndsTheSession()
    {
        var client = await factory.SignedInCustomer(UniqueEmail());

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
    public async Task SignedInCustomer_OrdersAreLinkedToTheAccount()
    {
        var client = await factory.SignedInCustomer(UniqueEmail());

        var order = await client.PlaceOrder(NewOrder(await client.ServiceTypeId(), FreshDay()));

        var mine = await client.GetAsync(OrdersUrl).ReadAs<List<CustomerOrderSummaryResponse>>();
        Assert.Equal(order.Reference, Assert.Single(mine).Reference);
    }

    [Fact]
    public async Task SeededAdmin_CanLogIn_AndUseTheAdminApi()
    {
        var client = factory.CreateClient(role: null);

        var admin = await client.Login(ApiFactory.SeededAdminEmail, ApiFactory.SeededAdminPassword).ReadAs<CurrentUserResponse>();

        Assert.Equal([Roles.Admin], admin.Roles);
        Assert.True(admin.IsStaff);
        await client.GetAsync("/api/admin/orders").AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_IsForbiddenFromTheAdminApi()
    {
        var client = await factory.SignedInCustomer(UniqueEmail());

        await client.GetAsync("/api/admin/orders").AssertStatus(HttpStatusCode.Forbidden);
    }
}
