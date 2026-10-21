using BouvetBike.Orders.Contracts;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Tests.Orders;

internal static class AdminOrderTestHelpers
{
    public const string AdminOrdersUrl = "/api/admin/orders";

    /// <summary>A bike brand no other test uses, to find exactly this test's orders by search.</summary>
    public static string UniqueBrand() => $"Brand{Guid.NewGuid():N}"[..20];

    public static Task<PagedResponse<AdminOrderSummaryResponse>> Search(this HttpClient staff, string query) =>
        staff.GetAsync($"{AdminOrdersUrl}?{query}").ReadAs<PagedResponse<AdminOrderSummaryResponse>>();

    /// <summary>The admin ID of an order placed through the customer API.</summary>
    public static async Task<Guid> AdminId(this HttpClient staff, CustomerOrderResponse order) =>
        (await staff.Search($"search={order.Reference}")).Items.Single().Id;

    public static Task<AdminOrderResponse> GetAdminOrder(this HttpClient staff, Guid id) =>
        staff.GetAsync($"{AdminOrdersUrl}/{id}").ReadAs<AdminOrderResponse>();
}
