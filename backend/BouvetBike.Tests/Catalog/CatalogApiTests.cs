using System.Net;
using System.Net.Http.Json;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Shared.Contracts;
using BouvetBike.Users;

namespace BouvetBike.Tests.Catalog;

/// <summary>
/// The catalog API: <c>/api/service-types</c>. REST resource with JSON Patch (RFC 6902) updates
/// and RFC 5789 status codes for PATCH. Tests share one database per class, so each test
/// creates the service types it changes.
/// </summary>
public class CatalogApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/service-types";

    private static readonly string[] SeededNames = ["Brake maintenance", "Chain replacement", "Wheel adjustment"];

    private readonly HttpClient _anonymous = factory.CreateClient(role: null);
    private readonly HttpClient _customer = factory.CreateClient(Roles.Customer);
    private readonly HttpClient _mechanic = factory.CreateClient(Roles.Mechanic);
    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);

    // ---- GET collection ----

    [Fact]
    public async Task List_IsPublic_AndSortedByName()
    {
        var services = await _anonymous.GetAsync(Url).ReadAs<List<ServiceTypeResponse>>();

        Assert.Superset(SeededNames.ToHashSet(), services.Select(s => s.Name).ToHashSet());
        Assert.Equal(services.Select(s => s.Name).Order(StringComparer.OrdinalIgnoreCase), services.Select(s => s.Name));
        Assert.All(services, s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task List_ShowsSeededPricesInNok()
    {
        var services = await _anonymous.GetAsync(Url).ReadAs<List<ServiceTypeResponse>>();

        var chain = Assert.Single(services, s => s.Name == "Chain replacement");
        Assert.Equal(new MoneyResponse(790, "NOK"), chain.Price);
        Assert.Equal(45, chain.EstimatedMinutes);
    }

    [Fact]
    public async Task List_HidesInactiveServiceTypes_FromCustomers()
    {
        var created = await Create(UniqueName("Retired service"));
        await _admin.PatchJsonAsync($"{Url}/{created.Id}", """[{ "op": "replace", "path": "/isActive", "value": false }]""")
            .AssertStatus(HttpStatusCode.OK);

        var forCustomers = await _anonymous.GetAsync(Url).ReadAs<List<ServiceTypeResponse>>();
        var forStaff = await _mechanic.GetAsync($"{Url}?includeInactive=true").ReadAs<List<ServiceTypeResponse>>();

        Assert.DoesNotContain(forCustomers, s => s.Id == created.Id);
        Assert.Contains(forStaff, s => s.Id == created.Id && !s.IsActive);
    }

    [Fact]
    public async Task List_IncludeInactive_RequiresLogin() =>
        await _anonymous.GetAsync($"{Url}?includeInactive=true").AssertStatus(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task List_IncludeInactive_IsForStaffOnly() =>
        await _customer.GetAsync($"{Url}?includeInactive=true").AssertStatus(HttpStatusCode.Forbidden);

    // ---- GET item ----

    [Fact]
    public async Task Get_ReturnsServiceType()
    {
        var created = await Create(UniqueName("Gear tune"), price: 350, minutes: 20, description: "Indexing and limits.");

        var fetched = await _anonymous.GetAsync($"{Url}/{created.Id}").ReadAs<ServiceTypeResponse>();

        Assert.Equal(created, fetched);
        Assert.Equal(("Indexing and limits.", new MoneyResponse(350, "NOK"), 20), (fetched.Description, fetched.Price, fetched.EstimatedMinutes));
    }

    [Fact]
    public async Task Get_Unknown_Is404() =>
        await _anonymous.GetAsync($"{Url}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    // ---- POST ----

    [Fact]
    public async Task Create_AsAdmin_Returns201_WithLocationOfNewResource()
    {
        var name = UniqueName("Tubeless setup");

        var response = await _admin.PostAsJsonAsync(Url, new CreateServiceTypeRequest(name, null, 590, 40));

        var created = await response.ReadAs<ServiceTypeResponse>(HttpStatusCode.Created);
        Assert.Equal((name, new MoneyResponse(590, "NOK"), 40, true), (created.Name, created.Price, created.EstimatedMinutes, created.IsActive));
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(created, await _anonymous.GetAsync(response.Headers.Location).ReadAs<ServiceTypeResponse>());
    }

    [Fact]
    public async Task Create_ServiceTakingSeveralDays_IsAllowed()
    {
        var threeDays = 3 * 24 * 60;

        var created = await _admin.PostAsJsonAsync(Url, new CreateServiceTypeRequest(UniqueName("Frame repair"), null, 2500, threeDays))
            .ReadAs<ServiceTypeResponse>(HttpStatusCode.Created);

        Assert.Equal(threeDays, created.EstimatedMinutes);
    }

    [Theory]
    [InlineData("""{ "price": 100, "estimatedMinutes": 30 }""", "name")]
    [InlineData("""{ "name": "X", "price": -1, "estimatedMinutes": 30 }""", "price")]
    [InlineData("""{ "name": "X", "estimatedMinutes": 30 }""", "price")]
    [InlineData("""{ "name": "X", "price": 100, "estimatedMinutes": 0 }""", "estimatedMinutes")]
    [InlineData("""{ "name": "X", "price": 100, "estimatedMinutes": 20161 }""", "estimatedMinutes")] // over two weeks
    public async Task Create_WithInvalidFields_Is400_WithFieldErrors(string body, string field)
    {
        var response = await _admin.PostAsync(Url, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        (await response.AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError(field);
    }

    [Fact]
    public async Task Create_WithExistingName_Is409_IgnoringCase() =>
        await _admin.PostAsJsonAsync(Url, new CreateServiceTypeRequest("chain REPLACEMENT", null, 100, 30))
            .AssertProblem(HttpStatusCode.Conflict);

    [Fact]
    public async Task Create_RequiresLogin() =>
        await _anonymous.PostAsJsonAsync(Url, new CreateServiceTypeRequest(UniqueName("X"), null, 100, 30))
            .AssertStatus(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData(Roles.Customer)]
    [InlineData(Roles.Mechanic)]
    public async Task Create_IsForAdminsOnly(string role) =>
        await factory.CreateClient(role).PostAsJsonAsync(Url, new CreateServiceTypeRequest(UniqueName("X"), null, 100, 30))
            .AssertStatus(HttpStatusCode.Forbidden);

    // ---- PATCH (RFC 6902 JSON Patch, RFC 5789 status codes) ----

    [Fact]
    public async Task Patch_Replace_UpdatesAndReturnsResource()
    {
        var created = await Create(UniqueName("Hub service"), price: 400);

        var patched = await _admin.PatchJsonAsync($"{Url}/{created.Id}", """
            [
              { "op": "replace", "path": "/price", "value": 450 },
              { "op": "replace", "path": "/estimatedMinutes", "value": 50 }
            ]
            """).ReadAs<ServiceTypeResponse>();

        Assert.Equal((new MoneyResponse(450, "NOK"), 50), (patched.Price, patched.EstimatedMinutes));
        Assert.Equal(patched, await _anonymous.GetAsync($"{Url}/{created.Id}").ReadAs<ServiceTypeResponse>());
    }

    [Fact]
    public async Task Patch_Remove_ClearsOptionalField()
    {
        var created = await Create(UniqueName("Bearing check"), description: "Something");

        var patched = await _admin.PatchJsonAsync($"{Url}/{created.Id}", """[{ "op": "remove", "path": "/description" }]""")
            .ReadAs<ServiceTypeResponse>();

        Assert.Null(patched.Description);
    }

    [Fact]
    public async Task Patch_PassingTestOperation_AppliesTheRest()
    {
        var created = await Create(UniqueName("Fork service"), price: 900);

        var patched = await _admin.PatchJsonAsync($"{Url}/{created.Id}", """
            [
              { "op": "test", "path": "/price", "value": 900 },
              { "op": "replace", "path": "/price", "value": 950 }
            ]
            """).ReadAs<ServiceTypeResponse>();

        Assert.Equal(950, patched.Price.Amount);
    }

    [Fact]
    public async Task Patch_FailingTestOperation_Is409_AndChangesNothing()
    {
        var created = await Create(UniqueName("Shock service"), price: 900);

        await _admin.PatchJsonAsync($"{Url}/{created.Id}", """
            [
              { "op": "replace", "path": "/name", "value": "Changed" },
              { "op": "test", "path": "/price", "value": 1 }
            ]
            """).AssertProblem(HttpStatusCode.Conflict);

        Assert.Equal(created, await Get(created.Id));
    }

    [Fact]
    public async Task Patch_InvalidValue_Is422_WithFieldError_AndChangesNothing()
    {
        var created = await Create(UniqueName("Wheel build"), price: 1500);

        var problem = await _admin.PatchJsonAsync($"{Url}/{created.Id}", """
            [
              { "op": "replace", "path": "/name", "value": "Changed" },
              { "op": "replace", "path": "/price", "value": -5 }
            ]
            """).AssertProblem(HttpStatusCode.UnprocessableEntity);

        problem.AssertFieldError("price");
        Assert.Equal(created, await Get(created.Id));
    }

    [Theory]
    [InlineData("""[{ "op": "replace", "path": "/doesNotExist", "value": 1 }]""")]
    [InlineData("""[{ "op": "replace", "path": "/id", "value": "00000000-0000-0000-0000-000000000001" }]""")]
    [InlineData("""[{ "op": "remove", "path": "/name" }]""")]
    public async Task Patch_UnknownReadOnlyOrRequiredPath_Is422(string patch)
    {
        var created = await Create(UniqueName("Cable swap"));

        await _admin.PatchJsonAsync($"{Url}/{created.Id}", patch).AssertProblem(HttpStatusCode.UnprocessableEntity);

        Assert.Equal(created, await Get(created.Id));
    }

    [Fact]
    public async Task Patch_RenameToExistingName_Is409()
    {
        var created = await Create(UniqueName("Pedal service"));

        await _admin.PatchJsonAsync($"{Url}/{created.Id}", """[{ "op": "replace", "path": "/name", "value": "Brake Maintenance" }]""")
            .AssertProblem(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("""{ "op": "replace", "path": "/price", "value": 1 }""")] // an object, not an array of operations
    [InlineData("""[{ "op": "frobnicate", "path": "/price" }]""")]
    [InlineData("not json")]
    public async Task Patch_MalformedDocument_Is400(string patch)
    {
        var created = await Create(UniqueName("Headset service"));

        await _admin.PatchJsonAsync($"{Url}/{created.Id}", patch).AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_WithPlainJsonContentType_Is415()
    {
        var created = await Create(UniqueName("Saddle fit"));

        await _admin.PatchJsonAsync($"{Url}/{created.Id}", """[{ "op": "replace", "path": "/price", "value": 1 }]""", "application/json")
            .AssertStatus(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Patch_Unknown_Is404() =>
        await _admin.PatchJsonAsync($"{Url}/{Guid.NewGuid()}", """[{ "op": "replace", "path": "/price", "value": 1 }]""")
            .AssertProblem(HttpStatusCode.NotFound);

    [Fact]
    public async Task Patch_RequiresLogin() =>
        await _anonymous.PatchJsonAsync($"{Url}/{Guid.NewGuid()}", """[{ "op": "replace", "path": "/price", "value": 1 }]""")
            .AssertStatus(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData(Roles.Customer)]
    [InlineData(Roles.Mechanic)]
    public async Task Patch_IsForAdminsOnly(string role) =>
        await factory.CreateClient(role).PatchJsonAsync($"{Url}/{Guid.NewGuid()}", """[{ "op": "replace", "path": "/price", "value": 1 }]""")
            .AssertStatus(HttpStatusCode.Forbidden);

    // ---- DELETE ----

    [Fact]
    public async Task Delete_IsNotAllowed_DeactivateInstead()
    {
        var created = await Create(UniqueName("Spoke repair"));

        await _admin.DeleteAsync($"{Url}/{created.Id}").AssertStatus(HttpStatusCode.MethodNotAllowed);
    }

    // ---- helpers ----

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private async Task<ServiceTypeResponse> Create(string name, decimal price = 500, int minutes = 30, string? description = null) =>
        await _admin.PostAsJsonAsync(Url, new CreateServiceTypeRequest(name, description, price, minutes))
            .ReadAs<ServiceTypeResponse>(HttpStatusCode.Created);

    private Task<ServiceTypeResponse> Get(Guid id) => _admin.GetAsync($"{Url}/{id}").ReadAs<ServiceTypeResponse>();
}
