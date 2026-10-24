using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Orders;

public class CapacityDomainTests
{
    private readonly TimeProvider _clock = Clock();

    [Fact]
    public void Schedule_StartsWithTheSameDefaultEveryDay()
    {
        var schedule = CapacitySchedule.Create(5);

        Assert.All(Enum.GetValues<DayOfWeek>(), day => Assert.Equal(5, schedule.ForDay(day)));
    }

    [Fact]
    public void Schedule_HasADefaultPerWeekday()
    {
        var schedule = CapacitySchedule.Create(5);

        schedule.Update(5, 5, 5, 5, 6, 3, 0);

        Assert.Equal((6, 3, 0, 5), (schedule.ForDay(DayOfWeek.Friday), schedule.ForDay(DayOfWeek.Saturday), schedule.ForDay(DayOfWeek.Sunday), schedule.ForDay(DayOfWeek.Monday)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(CapacitySchedule.MaxPerDay + 1)]
    public void Schedule_RejectsOutOfRangeValues(int value) =>
        Assert.Throws<DomainException>(() => CapacitySchedule.Create(5).Update(5, 5, 5, 5, 5, 5, value));

    [Fact]
    public void Override_CanAddRemoveOrCloseCapacity()
    {
        var date = Today.AddDays(10);

        var extra = CapacityOverride.Create(date, 6, "Extra mechanic", _clock);
        extra.Change(0, "Holiday", _clock);

        Assert.Equal((date, 0, "Holiday"), (extra.Date, extra.MaxOrders, extra.Reason));
    }

    [Fact]
    public void Override_ForToday_IsAllowed() =>
        CapacityOverride.Create(Today, 2, null, _clock);

    [Fact]
    public void Override_InThePast_IsRejected() =>
        Assert.Throws<DomainException>(() => CapacityOverride.Create(Today.AddDays(-1), 6, null, _clock));

    [Fact]
    public void Override_RejectsNegativeCapacity() =>
        Assert.Throws<DomainException>(() => CapacityOverride.Create(Today, -1, null, _clock));
}
