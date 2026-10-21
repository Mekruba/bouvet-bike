using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests;

/// <summary>Round-trips aggregates through SQLite to verify the EF mapping (complex types, converters, status history).</summary>
public class PersistenceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly TimeProvider _clock = Clock();

    private AppDbContext NewContext() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task ServiceTypes_AreSeeded()
    {
        await using var db = NewContext();

        Assert.Equal(3, await db.ServiceTypes.CountAsync());
    }

    [Fact]
    public async Task MaintenanceOrder_RoundTrips_WithEverything()
    {
        Guid orderId;
        await using (var db = NewContext())
        {
            var customer = new ApplicationUser { UserName = "kari@example.com", Email = "kari@example.com", Name = "Kari" };
            var staff = new ApplicationUser { UserName = "staff@example.com", Email = "staff@example.com", Name = "Ola" };
            db.Users.AddRange(customer, staff);
            var code = DiscountCode.Create("TEN", DiscountType.Percentage, 10);
            db.DiscountCodes.Add(code);

            var order = MaintenanceOrder.Place(
                Contact(), "Trek", "Domane", await db.ServiceTypes.FirstAsync(), Today.AddDays(2), _clock, customer.Id, "Squeaks");
            order.UpdateInternalNotes("Check rear hub too", _clock);
            order.ApplyDiscount(code.Redeem(_clock), _clock);
            order.ChangeStatus(OrderStatus.InProgress, staff.Id, _clock, "Started");
            order.OverridePrice(new Money(1000), "Loyal customer", staff.Id, _clock);
            db.MaintenanceOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = NewContext())
        {
            var order = await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId);

            Assert.Equal(Contact(), order.Contact);
            Assert.Equal(("Squeaks", "Check rear hub too"), (order.CustomerNotes, order.InternalNotes));
            Assert.Equal(OrderStatus.InProgress, Assert.Single(order.StatusHistory).To);
            Assert.Equal("TEN", order.Discount?.Code);
            Assert.Equal(new Money(1000), order.Total);
            Assert.NotNull(order.CustomerId);
            Assert.Equal(order, await db.MaintenanceOrders.SingleAsync(o => o.Reference == order.Reference));
        }
    }

    [Fact]
    public async Task GuestOrder_RoundTrips_WithNullOptionalParts()
    {
        Guid orderId;
        await using (var db = NewContext())
        {
            orderId = (await PlaceGuestOrder(db)).Id;
        }

        await using (var db = NewContext())
        {
            var order = await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId);
            Assert.Null(order.Discount);
            Assert.Null(order.PriceOverride);
            Assert.Null(order.CustomerId);
        }
    }

    [Fact]
    public async Task StatusChange_OnLoadedOrder_IsInserted()
    {
        Guid orderId;
        await using (var db = NewContext())
        {
            orderId = (await PlaceGuestOrder(db)).Id;
        }

        await using (var db = NewContext())
        {
            var order = await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId);
            order.ChangeStatus(OrderStatus.InProgress, null, _clock);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            Assert.Single((await db.MaintenanceOrders.SingleAsync(o => o.Id == orderId)).StatusHistory);
        }
    }

    private async Task<MaintenanceOrder> PlaceGuestOrder(AppDbContext db)
    {
        var order = MaintenanceOrder.Place(Contact(), "Giant", null, await db.ServiceTypes.FirstAsync(), Today, _clock);
        db.MaintenanceOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
