using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

public class OrderPricingTests
{
    private readonly TimeProvider _clock = Clock();

    [Fact]
    public void PercentageDiscount_IsSubtractedFromServicePrice()
    {
        var order = Order(_clock, Service(price: 800));

        order.ApplyDiscount(DiscountCode.Create("spring25", DiscountType.Percentage, 25).Redeem(_clock), _clock);

        Assert.Equal(new Money(200), order.DiscountAmount);
        Assert.Equal(new Money(600), order.Total);
    }

    [Fact]
    public void FixedDiscount_NeverExceedsServicePrice()
    {
        var order = Order(_clock, Service(price: 300));

        order.ApplyDiscount(DiscountCode.Create("BIG", DiscountType.FixedAmount, 500).Redeem(_clock), _clock);

        Assert.Equal(Money.Zero(), order.Total);
    }

    [Fact]
    public void Discount_IsRecalculated_WhenServiceChanges()
    {
        var order = Order(_clock, Service(price: 800));
        order.ApplyDiscount(DiscountCode.Create("TEN", DiscountType.Percentage, 10).Redeem(_clock), _clock);

        order.ChangeService(Service("Brake maintenance", 500), _clock);

        Assert.Equal(new Money(450), order.Total);
    }

    [Fact]
    public void OnlyOneDiscountPerOrder()
    {
        var order = Order(_clock);
        order.ApplyDiscount(DiscountCode.Create("A", DiscountType.Percentage, 10).Redeem(_clock), _clock);

        Assert.Throws<StateConflictException>(() =>
            order.ApplyDiscount(DiscountCode.Create("B", DiscountType.Percentage, 10).Redeem(_clock), _clock));
    }

    [Fact]
    public void PriceOverride_ReplacesCalculatedTotal_AndIsAllowedOnCompletedOrder()
    {
        var order = Order(_clock, Service(price: 800));
        order.ChangeStatus(OrderStatus.InProgress, null, _clock);
        order.ChangeStatus(OrderStatus.Completed, null, _clock);
        var staffId = Guid.NewGuid();

        order.OverridePrice(new Money(650), "Chain was fine, only cleaned", staffId, _clock);

        Assert.Equal(new Money(650), order.Total);
        Assert.Equal(staffId, order.PriceOverride!.OverriddenById);

        order.ClearPriceOverride(_clock);
        Assert.Equal(new Money(800), order.Total);
    }

    [Fact]
    public void PriceOverride_RequiresReason() =>
        Assert.Throws<DomainException>(() => Order(_clock).OverridePrice(new Money(100), " ", Guid.NewGuid(), _clock));

    [Fact]
    public void PriceOverride_NotAllowedOnCancelledOrder()
    {
        var order = Order(_clock);
        order.ChangeStatus(OrderStatus.Cancelled, null, _clock);

        Assert.Throws<StateConflictException>(() => order.OverridePrice(new Money(100), "Goodwill", Guid.NewGuid(), _clock));
    }
}
