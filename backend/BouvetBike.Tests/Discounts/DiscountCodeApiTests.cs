using System.Net;
using BouvetBike.Discounts.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Tests.Orders;
using BouvetBike.Users;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Discounts;

/// <summary>
/// Admin management of discount codes: <c>/api/admin/discount-codes</c>. Same REST shape as the catalog:
/// no DELETE (orders keep the codes they used), retire with JSON Patch <c>/isActive</c>.
/// </summary>
public class DiscountCodeApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/admin/discount-codes";
    private readonly HttpClient _admin = factory.CreateClient(Roles.Admin);
    private readonly HttpClient _anonymous = factory.CreateClient(role: null);

    // ---- create and read ----

    [Fact]
    public async Task Create_Returns201_WithLocation_CodeUpperCased()
    {
        var code = UniqueCode().ToLowerInvariant();

        var response = await _admin.PostJsonAsync(Url, new CreateDiscountCodeRequest(code, DiscountType.Percentage, 15, Today, Today.AddDays(30), 10));

        var created = await response.ReadAs<DiscountCodeResponse>(HttpStatusCode.Created);
        Assert.Equal(
            (code.ToUpperInvariant(), DiscountType.Percentage, 15m, Today, Today.AddDays(30), 10, 0, true),
            (created.Code, created.Type, created.Value, created.ValidFrom, created.ValidTo, created.MaxUses, created.TimesUsed, created.IsActive));
        Assert.EndsWith($"{Url}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(created, await _admin.GetAsync(response.Headers.Location!.OriginalString).ReadAs<DiscountCodeResponse>());
    }

    [Fact]
    public async Task CreatedCode_IsUsableRightAway()
    {
        var code = await Create(DiscountType.FixedAmount, 200);

        var serviceId = await _anonymous.ServiceTypeId("Chain replacement");
        var quote = await _anonymous.GetAsync($"/api/price-quotes?serviceTypeId={serviceId}&discountCode={code.Code}").ReadAs<OrderPriceResponse>();

        Assert.Equal(590m, quote.Total.Amount);
    }

    [Fact]
    public async Task List_ShowsAllCodes_IncludingInactive_SortedByCode()
    {
        var active = await Create();
        var retired = await Create();
        await Patch(retired.Id, """[{ "op": "replace", "path": "/isActive", "value": false }]""").AssertStatus(HttpStatusCode.OK);

        var codes = await _admin.GetAsync(Url).ReadAs<List<DiscountCodeResponse>>();

        Assert.Contains(codes, c => c.Id == active.Id && c.IsActive);
        Assert.Contains(codes, c => c.Id == retired.Id && !c.IsActive);
        Assert.Equal(codes.Select(c => c.Code).Order(StringComparer.Ordinal), codes.Select(c => c.Code));
    }

    [Fact]
    public async Task List_ShowsHowOftenACodeWasUsed()
    {
        var code = await Create();
        var client = factory.CreateClient(role: null);
        await client.PlaceOrder(OrderTestHelpers.NewOrder(await client.ServiceTypeId(), OrderTestHelpers.FreshDay(), discountCode: code.Code));

        Assert.Equal(1, (await Get(code.Id)).TimesUsed);
    }

    [Fact]
    public async Task Create_WithExistingCode_Is409_IgnoringCase()
    {
        var code = await Create();

        await _admin.PostJsonAsync(Url, new CreateDiscountCodeRequest(code.Code.ToLowerInvariant(), DiscountType.Percentage, 5))
            .AssertProblem(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("""{ "type": "Percentage", "value": 10 }""", "code")]
    [InlineData("""{ "code": "has space", "type": "Percentage", "value": 10 }""", "code")]
    [InlineData("""{ "code": "OK", "value": 10 }""", "type")]
    [InlineData("""{ "code": "OK", "type": "Percentage" }""", "value")]
    [InlineData("""{ "code": "OK", "type": "Percentage", "value": 10, "maxUses": 0 }""", "maxUses")]
    public async Task Create_WithInvalidFields_Is400_WithFieldError(string body, string field)
    {
        var response = await _admin.PostAsync(Url, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        (await response.AssertProblem(HttpStatusCode.BadRequest)).AssertFieldError(field);
    }

    [Fact]
    public async Task Create_PercentageOver100_Is400() =>
        await _admin.PostJsonAsync(Url, new CreateDiscountCodeRequest(UniqueCode(), DiscountType.Percentage, 150))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Create_EndingBeforeItStarts_Is400() =>
        await _admin.PostJsonAsync(Url, new CreateDiscountCodeRequest(UniqueCode(), DiscountType.Percentage, 10, Today, Today.AddDays(-1)))
            .AssertProblem(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Get_Unknown_Is404() =>
        await _admin.GetAsync($"{Url}/{Guid.NewGuid()}").AssertProblem(HttpStatusCode.NotFound);

    // ---- PATCH ----

    [Fact]
    public async Task Patch_Deactivate_MakesTheCodeUnusable()
    {
        var code = await Create();

        var patched = await Patch(code.Id, """[{ "op": "replace", "path": "/isActive", "value": false }]""").ReadAs<DiscountCodeResponse>();

        Assert.False(patched.IsActive);
        var serviceId = await _anonymous.ServiceTypeId();
        await _anonymous.GetAsync($"/api/price-quotes?serviceTypeId={serviceId}&discountCode={code.Code}").AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_Terms()
    {
        var code = await Create(DiscountType.Percentage, 10);

        var patched = await Patch(code.Id, $$"""
            [
              { "op": "replace", "path": "/type", "value": "FixedAmount" },
              { "op": "replace", "path": "/value", "value": 250 },
              { "op": "add", "path": "/validTo", "value": "{{Today.AddDays(7):yyyy-MM-dd}}" },
              { "op": "add", "path": "/maxUses", "value": 3 }
            ]
            """).ReadAs<DiscountCodeResponse>();

        Assert.Equal((DiscountType.FixedAmount, 250m, Today.AddDays(7), 3), (patched.Type, patched.Value, patched.ValidTo, patched.MaxUses));
    }

    [Fact]
    public async Task Patch_InvalidResultingTerms_Is422_AndChangesNothing()
    {
        var code = await Create(DiscountType.FixedAmount, 500);

        await Patch(code.Id, """[{ "op": "replace", "path": "/type", "value": "Percentage" }]""") // 500 % is not a valid percentage
            .AssertProblem(HttpStatusCode.UnprocessableEntity);

        Assert.Equal(code, await Get(code.Id));
    }

    [Fact]
    public async Task Patch_MaxUsesBelowTimesUsed_Is422()
    {
        var code = await Create();
        var client = factory.CreateClient(role: null);
        await client.PlaceOrder(OrderTestHelpers.NewOrder(await client.ServiceTypeId(), OrderTestHelpers.FreshDay(), discountCode: code.Code));
        await client.PlaceOrder(OrderTestHelpers.NewOrder(await client.ServiceTypeId(), OrderTestHelpers.FreshDay(), discountCode: code.Code));

        await Patch(code.Id, """[{ "op": "add", "path": "/maxUses", "value": 1 }]""").AssertProblem(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("""[{ "op": "replace", "path": "/code", "value": "RENAMED" }]""")]
    [InlineData("""[{ "op": "replace", "path": "/timesUsed", "value": 0 }]""")]
    [InlineData("""[{ "op": "replace", "path": "/value", "value": 0 }]""")]
    public async Task Patch_ReadOnlyPathOrInvalidValue_Is422(string patch) =>
        await Patch((await Create()).Id, patch).AssertProblem(HttpStatusCode.UnprocessableEntity);

    [Fact]
    public async Task Patch_FailingTestOperation_Is409() =>
        await Patch((await Create(value: 10)).Id, """[{ "op": "test", "path": "/value", "value": 99 }]""").AssertProblem(HttpStatusCode.Conflict);

    [Fact]
    public async Task Patch_WithPlainJsonContentType_Is415() =>
        await _admin.PatchJsonAsync($"{Url}/{(await Create()).Id}", """[{ "op": "replace", "path": "/value", "value": 5 }]""", "application/json")
            .AssertStatus(HttpStatusCode.UnsupportedMediaType);

    [Fact]
    public async Task Patch_Unknown_Is404() =>
        await Patch(Guid.NewGuid(), """[{ "op": "replace", "path": "/value", "value": 5 }]""").AssertProblem(HttpStatusCode.NotFound);

    // ---- DELETE and access ----

    [Fact]
    public async Task Delete_IsNotAllowed_DeactivateInstead() =>
        await _admin.DeleteAsync($"{Url}/{(await Create()).Id}").AssertStatus(HttpStatusCode.MethodNotAllowed);

    [Fact]
    public async Task RequiresLogin() =>
        await _anonymous.GetAsync(Url).AssertStatus(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData(Roles.Mechanic)]
    [InlineData(Roles.Customer)]
    public async Task IsForAdminsOnly(string role)
    {
        var client = factory.CreateClient(role);

        await client.GetAsync(Url).AssertStatus(HttpStatusCode.Forbidden);
        await client.PostJsonAsync(Url, new CreateDiscountCodeRequest(UniqueCode(), DiscountType.Percentage, 5)).AssertStatus(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private static string UniqueCode() => $"T{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    private Task<DiscountCodeResponse> Create(DiscountType type = DiscountType.Percentage, decimal value = 10) =>
        _admin.PostJsonAsync(Url, new CreateDiscountCodeRequest(UniqueCode(), type, value)).ReadAs<DiscountCodeResponse>(HttpStatusCode.Created);

    private Task<DiscountCodeResponse> Get(Guid id) => _admin.GetAsync($"{Url}/{id}").ReadAs<DiscountCodeResponse>();

    private Task<HttpResponseMessage> Patch(Guid id, string patch) => _admin.PatchJsonAsync($"{Url}/{id}", patch);
}
