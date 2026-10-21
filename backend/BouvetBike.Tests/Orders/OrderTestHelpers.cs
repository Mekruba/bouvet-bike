using System.Net;
using BouvetBike.Catalog.Contracts;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Tests.Orders;

/// <summary>Shared setup for order API tests: valid requests, placing orders, and data with no API yet.</summary>
internal static class OrderTestHelpers
{
    public const string OrdersUrl = "/api/orders";

    private static int _dayOffset = 100;

    /// <summary>
    /// A due date no other test uses, for tests that aren't about capacity: tests in a class share a
    /// database, and more than 5 orders on one day would make the next one fail as fully booked.
    /// </summary>
    public static DateOnly FreshDay() => TestData.Today.AddDays(Interlocked.Increment(ref _dayOffset));

    public static PlaceOrderRequest NewOrder(
        Guid serviceTypeId,
        DateOnly dueDate,
        string? discountCode = null,
        string email = "kari@example.com",
        string phone = "912 34 567",
        string? notes = null) =>
        new("Kari Nordmann", email, phone, "Trek", "Domane SL5", serviceTypeId, dueDate, notes, discountCode);

    public static async Task<Guid> ServiceTypeId(this HttpClient client, string name = "Chain replacement")
    {
        var services = await client.GetAsync("/api/service-types").ReadAs<List<ServiceTypeResponse>>();
        return services.Single(s => s.Name == name).Id;
    }

    public static async Task<CustomerOrderResponse> PlaceOrder(this HttpClient client, PlaceOrderRequest request) =>
        await client.PostJsonAsync(OrdersUrl, request).ReadAs<CustomerOrderResponse>(HttpStatusCode.Created);

    public static async Task PlaceOrders(this HttpClient client, Guid serviceTypeId, DateOnly dueDate, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await client.PlaceOrder(NewOrder(serviceTypeId, dueDate));
        }
    }

    public static Task SeedDiscountCode(this ApiFactory factory, string code, DiscountType type, decimal value, int? maxUses = null) =>
        factory.WithDbAsync(db =>
        {
            db.DiscountCodes.Add(DiscountCode.Create(code, type, value, maxUses: maxUses));
            return Task.CompletedTask;
        });

    /// <summary>Cancels an order directly in the database (the admin status API comes later).</summary>
    public static Task CancelOrder(this ApiFactory factory, string reference) =>
        factory.WithDbAsync(async db =>
        {
            var order = await db.MaintenanceOrders.SingleAsync(o => o.Reference == OrderReference.Parse(reference));
            order.ChangeStatus(OrderStatus.Cancelled, null, factory.Clock);
        });
}
