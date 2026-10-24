using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>
/// The default daily capacity for each weekday (0 = closed). One schedule for the shop; a date can deviate
/// from it with a <see cref="CapacityOverride"/>.
/// </summary>
public sealed class CapacitySchedule : AggregateRoot
{
    public const int MaxPerDay = 1000;

    public int Monday { get; private set; }
    public int Tuesday { get; private set; }
    public int Wednesday { get; private set; }
    public int Thursday { get; private set; }
    public int Friday { get; private set; }
    public int Saturday { get; private set; }
    public int Sunday { get; private set; }

    private CapacitySchedule() { } // EF Core

    /// <summary>Every weekday starts with the same default.</summary>
    public static CapacitySchedule Create(int everyDay) => throw new NotImplementedException("Not yet test-driven.");

    public int ForDay(DayOfWeek day) => throw new NotImplementedException("Not yet test-driven.");

    public void Update(int monday, int tuesday, int wednesday, int thursday, int friday, int saturday, int sunday) =>
        throw new NotImplementedException("Not yet test-driven.");
}
