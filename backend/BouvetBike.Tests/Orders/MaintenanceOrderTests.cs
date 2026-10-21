using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

public class MaintenanceOrderTests
{
    private readonly TimeProvider _clock = Clock();

    [Fact]
    public void Place_StartsAsNew_WithServicePricedFromCatalog()
    {
        var service = Service("Chain replacement", 790);

        var order = MaintenanceOrder.Place(Contact(), " Trek ", null, service, Today, _clock, customerNotes: "Squeaks");

        Assert.Equal(OrderStatus.New, order.Status);
        Assert.StartsWith(OrderReference.Prefix, order.Reference.Value);
        Assert.Equal("Trek", order.BikeBrand);
        Assert.Equal((service.Id, "Chain replacement"), (order.ServiceTypeId, order.ServiceName));
        Assert.Equal(new Money(790), order.Total);
        Assert.Equal("Squeaks", order.CustomerNotes);
        Assert.Empty(order.StatusHistory);
    }

    [Fact]
    public void Place_WithInactiveService_Throws()
    {
        var service = Service();
        service.Deactivate();

        Assert.Throws<DomainException>(() => Order(_clock, service));
    }

    [Fact]
    public void Place_WithoutBikeBrand_Throws() =>
        Assert.Throws<DomainException>(() => MaintenanceOrder.Place(Contact(), " ", null, Service(), Today, _clock));

    [Fact]
    public void Place_WithPastDueDate_Throws() =>
        Assert.Throws<DomainException>(() =>
            MaintenanceOrder.Place(Contact(), "Trek", null, Service(), Today.AddDays(-1), _clock));

    [Fact]
    public void Place_WithDueDateToday_IsAllowed() =>
        MaintenanceOrder.Place(Contact(), "Trek", null, Service(), Today, _clock);

    [Theory]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.WaitingForCustomer)]
    [InlineData(OrderStatus.Cancelled)]
    public void ChangeStatus_FromNew_ToAllowedStatus_RecordsHistory(OrderStatus to)
    {
        var order = Order(_clock);
        var staffId = Guid.NewGuid();

        order.ChangeStatus(to, staffId, _clock, "reason");

        Assert.Equal(to, order.Status);
        var change = Assert.Single(order.StatusHistory);
        Assert.Equal((OrderStatus.New, to, staffId, "reason"), (change.From, change.To, change.ChangedById, change.Reason));
    }

    [Theory]
    [InlineData(new[] { OrderStatus.Completed })]
    [InlineData(new[] { OrderStatus.New })]
    [InlineData(new[] { OrderStatus.InProgress, OrderStatus.New })]
    [InlineData(new[] { OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.InProgress })]
    [InlineData(new[] { OrderStatus.Cancelled, OrderStatus.New })]
    public void ChangeStatus_InvalidTransition_Throws(OrderStatus[] path)
    {
        var order = Order(_clock);
        foreach (var status in path[..^1])
        {
            order.ChangeStatus(status, null, _clock);
        }

        Assert.Throws<DomainException>(() => order.ChangeStatus(path[^1], null, _clock));
        Assert.Equal(path.Length - 1, order.StatusHistory.Count);
    }

    [Fact]
    public void CustomerNotes_CanBeChanged_WhileNew()
    {
        var order = Order(_clock);

        order.UpdateCustomerNotes("Front wheel wobbles", _clock);
        Assert.Equal("Front wheel wobbles", order.CustomerNotes);

        order.UpdateCustomerNotes("  ", _clock);
        Assert.Null(order.CustomerNotes);
    }

    [Theory]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.WaitingForCustomer)]
    [InlineData(OrderStatus.Cancelled)]
    public void CustomerNotes_AreLocked_OnceNoLongerNew(OrderStatus status)
    {
        var order = Order(_clock);
        order.ChangeStatus(status, null, _clock);

        Assert.Throws<DomainException>(() => order.UpdateCustomerNotes("More info", _clock));
    }

    [Fact]
    public void InternalNotes_CanBeChanged_AfterCompletion()
    {
        var order = Order(_clock);
        order.ChangeStatus(OrderStatus.InProgress, null, _clock);
        order.ChangeStatus(OrderStatus.Completed, null, _clock);

        order.UpdateInternalNotes("Picked up", _clock);

        Assert.Equal("Picked up", order.InternalNotes);
    }

    [Fact]
    public void ChangeService_RepricesFromCatalog_ButSameServiceKeepsOriginalPrice()
    {
        var chain = Service("Chain replacement", 790);
        var order = Order(_clock, chain);
        chain.Update(chain.Name, null, new Money(990), chain.EstimatedDuration);

        order.ChangeService(chain, _clock);
        Assert.Equal(new Money(790), order.ServicePrice);

        order.ChangeService(Service("Brake maintenance", 690), _clock);
        Assert.Equal(("Brake maintenance", new Money(690)), (order.ServiceName, order.ServicePrice));
    }

    [Fact]
    public void ClosedOrder_CannotBeEdited()
    {
        var order = Order(_clock);
        order.ChangeStatus(OrderStatus.Cancelled, null, _clock);

        Assert.Throws<DomainException>(() => order.UpdateDetails(Contact(), "Trek", null, Today, _clock));
        Assert.Throws<DomainException>(() => order.ChangeService(Service("Other"), _clock));
    }

    [Fact]
    public void UpdateDetails_AllowsUnchangedPastDueDate()
    {
        var clock = Clock();
        var order = Order(clock);
        clock.Advance(TimeSpan.FromDays(10));

        order.UpdateDetails(Contact(), "Trek", "New model", order.DueDate, clock);

        Assert.Equal("New model", order.BikeModel);
    }

    [Fact]
    public void UpdateDetails_WithNewPastDueDate_ThrowsAndChangesNothing()
    {
        var order = Order(_clock);

        Assert.Throws<DomainException>(() => order.UpdateDetails(Contact(), "Giant", null, Today.AddDays(-1), _clock));
        Assert.Equal("Trek", order.BikeBrand);
    }
}
