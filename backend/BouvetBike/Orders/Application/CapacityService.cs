using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BouvetBike.Orders.Application;

/// <summary>How many orders each date takes: weekday defaults, per-date overrides, and the bookings counted against them.</summary>
public sealed class CapacityService(AppDbContext db, IOptions<CapacityOptions> options, TimeProvider clock) : ICapacityService
{
    private const int DefaultOverrideRangeDays = 60;

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().Date);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DayCapacityResponse>> GetAsync(CapacityQuery query, CancellationToken ct)
    {
        var from = query.From ?? Today;
        var to = from.AddDays(query.Days - 1);
        var capacity = await CapacityCalendarAsync(from, to, ct);
        var booked = await CountPerDayAsync(from, to, ignoreOrderId: null, ct);

        return Enumerable.Range(0, query.Days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new DayCapacityResponse(date, booked.GetValueOrDefault(date), capacity.For(date), capacity.DefaultFor(date)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task EnsureAvailableAsync(DateOnly date, Guid? ignoreOrderId, CancellationToken ct)
    {
        var searchEnd = date.AddDays(options.Value.SearchDays);
        var capacity = await CapacityCalendarAsync(date, searchEnd, ct);
        var booked = await CountPerDayAsync(date, searchEnd, ignoreOrderId, ct);

        // Whether the day can take one more order: fewer bookings than its capacity (a closed day never can).
        bool HasRoom(DateOnly day) => booked.GetValueOrDefault(day) < capacity.For(day);

        if (HasRoom(date))
        {
            return;
        }

        for (var next = date.AddDays(1); next <= searchEnd; next = next.AddDays(1))
        {
            if (HasRoom(next))
            {
                throw new DueDateFullException(date, next);
            }
        }

        throw new DomainException(
            $"{date:yyyy-MM-dd} is fully booked, and so is every day in the {options.Value.SearchDays} days after it.");
    }

    /// <inheritdoc />
    public async Task<CapacityScheduleResponse> GetScheduleAsync(CancellationToken ct) =>
        ToResponse(await ScheduleAsync(ct));

    /// <inheritdoc />
    public async Task<CapacityScheduleResponse> PatchScheduleAsync(JsonPatchDocument<CapacitySchedulePatch> patch, CancellationToken ct)
    {
        var schedule = await ScheduleAsync(ct);
        var model = new CapacitySchedulePatch
        {
            Monday = schedule.Monday,
            Tuesday = schedule.Tuesday,
            Wednesday = schedule.Wednesday,
            Thursday = schedule.Thursday,
            Friday = schedule.Friday,
            Saturday = schedule.Saturday,
            Sunday = schedule.Sunday,
        };

        patch.ApplyAndValidate(model);
        try
        {
            schedule.Update(model.Monday, model.Tuesday, model.Wednesday, model.Thursday, model.Friday, model.Saturday, model.Sunday);
        }
        catch (DomainException ex) when (ex is not StateConflictException)
        {
            throw UnprocessableException.From(ex); // RFC 5789: a patch that would leave the resource invalid is 422
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(schedule);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CapacityOverrideResponse>> GetOverridesAsync(CapacityOverrideQuery query, CancellationToken ct)
    {
        var from = query.From ?? Today;
        var to = query.To ?? from.AddDays(DefaultOverrideRangeDays);
        var schedule = await ScheduleAsync(ct);
        var overrides = await db.CapacityOverrides.Where(o => o.Date >= from && o.Date <= to).OrderBy(o => o.Date).ToListAsync(ct);
        return overrides.Select(o => ToResponse(o, schedule)).ToList();
    }

    /// <inheritdoc />
    public async Task<(CapacityOverrideResponse Override, bool Created)> SetOverrideAsync(
        DateOnly date, SetCapacityOverrideRequest request, CancellationToken ct)
    {
        var existing = await db.CapacityOverrides.SingleOrDefaultAsync(o => o.Date == date, ct);
        if (existing is null)
        {
            existing = CapacityOverride.Create(date, request.MaxOrders!.Value, request.Reason, clock);
            db.CapacityOverrides.Add(existing);
        }
        else
        {
            existing.Change(request.MaxOrders!.Value, request.Reason, clock);
        }

        var created = db.Entry(existing).State == EntityState.Added;
        await db.SaveChangesAsync(ct);
        return (ToResponse(existing, await ScheduleAsync(ct)), created);
    }

    /// <inheritdoc />
    public async Task RemoveOverrideAsync(DateOnly date, CancellationToken ct)
    {
        if (await db.CapacityOverrides.SingleOrDefaultAsync(o => o.Date == date, ct) is { } existing)
        {
            db.CapacityOverrides.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// The schedule, created from configuration if it doesn't exist yet. Normally it's created when the database
    /// is seeded, so concurrent first requests can't create two.
    /// </summary>
    private async Task<CapacitySchedule> ScheduleAsync(CancellationToken ct)
    {
        if (await db.CapacitySchedules.FirstOrDefaultAsync(ct) is { } schedule)
        {
            return schedule;
        }

        schedule = CapacitySchedule.Create(options.Value.MaxOrdersPerDay);
        db.CapacitySchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return schedule;
    }

    /// <summary>Capacity per date in a range: the override if there is one, otherwise the weekday default.</summary>
    private async Task<CapacityCalendar> CapacityCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var schedule = await ScheduleAsync(ct);
        var overrides = await db.CapacityOverrides
            .Where(o => o.Date >= from && o.Date <= to)
            .ToDictionaryAsync(o => o.Date, o => o.MaxOrders, ct);
        return new CapacityCalendar(schedule, overrides);
    }

    /// <summary>The schedule and the overrides in a date range, to look up any date in it without more queries.</summary>
    private sealed record CapacityCalendar(CapacitySchedule Schedule, Dictionary<DateOnly, int> Overrides)
    {
        /// <summary>The weekday default for the date, ignoring overrides.</summary>
        public int DefaultFor(DateOnly date) => Schedule.ForDay(date.DayOfWeek);

        /// <summary>The capacity that applies on the date: its override, or the weekday default.</summary>
        public int For(DateOnly date) => Overrides.TryGetValue(date, out var capacity) ? capacity : DefaultFor(date);
    }

    /// <summary>Non-cancelled orders per due date in [from, to].</summary>
    private async Task<Dictionary<DateOnly, int>> CountPerDayAsync(DateOnly from, DateOnly to, Guid? ignoreOrderId, CancellationToken ct) =>
        await db.MaintenanceOrders
            .Where(o => o.DueDate >= from && o.DueDate <= to && o.Status != OrderStatus.Cancelled && o.Id != ignoreOrderId)
            .GroupBy(o => o.DueDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

    /// <summary>The weekday defaults as the API shows them.</summary>
    private static CapacityScheduleResponse ToResponse(CapacitySchedule s) =>
        new(s.Monday, s.Tuesday, s.Wednesday, s.Thursday, s.Friday, s.Saturday, s.Sunday);

    /// <summary>An override next to its weekday default, so clients can show e.g. "6/5".</summary>
    private static CapacityOverrideResponse ToResponse(CapacityOverride o, CapacitySchedule schedule) =>
        new(o.Date, o.MaxOrders, schedule.ForDay(o.Date.DayOfWeek), o.Reason);
}
