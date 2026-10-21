using System.Net;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Users;
using BouvetBike.Users.Contracts;

namespace BouvetBike.Tests.Users;

/// <summary>Admins manage staff accounts: <c>GET</c>/<c>POST /api/admin/staff</c>, <c>GET /api/admin/staff/{id}</c>.</summary>
public class StaffApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/admin/staff";
    private const string Password = "Verksted-2026";

    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);

    [Fact]
    public async Task Create_Mechanic_Returns201_WithLocation()
    {
        var email = UniqueEmail();

        var response = await _admin.PostJsonAsync(Url, new CreateStaffRequest("Ola Mekaniker", email, Password, Roles.Mechanic));

        var created = await response.ReadAs<StaffUserResponse>(HttpStatusCode.Created);
        Assert.Equal(("Ola Mekaniker", email), (created.Name, created.Email));
        Assert.Equal([Roles.Mechanic], created.Roles);
        Assert.EndsWith($"{Url}/{created.Id}", response.Headers.Location?.OriginalString);
        ApiAssert.AssertSameJson(created, await _admin.GetAsync(response.Headers.Location!.OriginalString).ReadAs<StaffUserResponse>());
    }

    [Fact]
    public async Task CreatedMechanic_CanLogIn_WithStaffButNotAdminRights()
    {
        var email = UniqueEmail();
        await _admin.PostJsonAsync(Url, new CreateStaffRequest("Ola", email, Password, Roles.Mechanic)).AssertStatus(HttpStatusCode.Created);
        var mechanic = factory.CreateClient(role: null);

        await mechanic.PostJsonAsync("/api/session", new LoginRequest(email, Password)).AssertStatus(HttpStatusCode.OK);

        await mechanic.GetAsync("/api/admin/orders").AssertStatus(HttpStatusCode.OK);
        await mechanic.PostJsonAsync("/api/service-types", new CreateServiceTypeRequest("Not allowed", null, 1, 1))
            .AssertStatus(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ShowsStaff_NotCustomers()
    {
        var mechanicEmail = UniqueEmail();
        var customerEmail = UniqueEmail();
        await _admin.PostJsonAsync(Url, new CreateStaffRequest("Ola", mechanicEmail, Password, Roles.Mechanic)).AssertStatus(HttpStatusCode.Created);
        await factory.CreateClient(role: null).PostJsonAsync("/api/users", new RegisterRequest("Kari", customerEmail, null, Password))
            .AssertStatus(HttpStatusCode.Created);

        var staff = await _admin.GetAsync(Url).ReadAs<List<StaffUserResponse>>();

        Assert.Contains(staff, s => s.Email == mechanicEmail);
        Assert.Contains(staff, s => s.Email == ApiFactory.SeededAdminEmail);
        Assert.DoesNotContain(staff, s => s.Email == customerEmail);
    }

    [Fact]
    public async Task Create_WithTakenEmail_Is409() =>
        await _admin.PostJsonAsync(Url, new CreateStaffRequest("Copy", ApiFactory.SeededAdminEmail, Password, Roles.Admin))
            .AssertProblem(HttpStatusCode.Conflict);

    [Fact]
    public async Task Create_WithCustomerRole_Is400() =>
        (await _admin.PostJsonAsync(Url, new CreateStaffRequest("Kari", UniqueEmail(), Password, Roles.Customer))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("role");

    [Fact]
    public async Task Create_WithWeakPassword_Is400() =>
        (await _admin.PostJsonAsync(Url, new CreateStaffRequest("Ola", UniqueEmail(), "alllowercase-no-digits", Roles.Mechanic))
            .AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError("password");

    [Fact]
    public async Task Get_Unknown_Is404() =>
        await _admin.GetAsync($"{Url}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task Get_Customer_Is404()
    {
        var customer = await factory.CreateClient(role: null).PostJsonAsync("/api/users", new RegisterRequest("Kari", UniqueEmail(), null, Password))
            .ReadAs<CurrentUserResponse>(HttpStatusCode.Created);

        await _admin.GetAsync($"{Url}/{customer.Id}").AssertProblem(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Roles.Mechanic)]
    [InlineData(Roles.Customer)]
    public async Task ManagingStaff_IsForAdminsOnly(string role)
    {
        var client = factory.CreateClient(role);

        await client.GetAsync(Url).AssertStatus(HttpStatusCode.Forbidden);
        await client.PostJsonAsync(Url, new CreateStaffRequest("X", UniqueEmail(), Password, Roles.Mechanic)).AssertStatus(HttpStatusCode.Forbidden);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@bouvetbike.test";
}
