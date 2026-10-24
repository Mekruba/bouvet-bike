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
    public static CapacitySchedule Create(int everyDay)
    {
        var schedule = new CapacitySchedule();
        schedule.Update(everyDay, everyDay, everyDay, everyDay, everyDay, everyDay, everyDay);
        return schedule;
    }

    /// <summary>The default capacity for the weekday.</summary>
    public int ForDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        _ => Sunday,
    };

    /// <summary>Replaces all seven defaults at once; nothing changes if any value is invalid.</summary>
    /// <exception cref="DomainException">A value is outside 0 to <see cref="MaxPerDay"/>.</exception>
    public void Update(int monday, int tuesday, int wednesday, int thursday, int friday, int saturday, int sunday)
    {
        int[] values = [monday, tuesday, wednesday, thursday, friday, saturday, sunday];
        if (values.Any(v => v is < 0 or > MaxPerDay))
        {
            throw new DomainException($"Daily capacity must be between 0 (closed) and {MaxPerDay}.");
        }

        (Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday) = (monday, tuesday, wednesday, thursday, friday, saturday, sunday);
    }
}
