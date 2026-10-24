using BouvetBike.Orders.Contracts;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BouvetBike.Orders.Application;

public sealed class CapacityService(AppDbContext db, IOptions<CapacityOptions> options, TimeProvider clock) : ICapacityService
{
    private int MaxPerDay => options.Value.MaxOrdersPerDay;

    public async Task<IReadOnlyList<DayCapacityResponse>> GetAsync(CapacityQuery query, CancellationToken ct)
    {
        var from = query.From ?? DateOnly.FromDateTime(clock.GetLocalNow().Date);
        var booked = await CountPerDayAsync(from, from.AddDays(query.Days - 1), ignoreOrderId: null, ct);

        return Enumerable.Range(0, query.Days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new DayCapacityResponse(date, booked.GetValueOrDefault(date), MaxPerDay, MaxPerDay))
            .ToList();
    }

    public async Task EnsureAvailableAsync(DateOnly date, Guid? ignoreOrderId, CancellationToken ct)
    {
        var searchEnd = date.AddDays(options.Value.SearchDays);
        var booked = await CountPerDayAsync(date, searchEnd, ignoreOrderId, ct);
        if (booked.GetValueOrDefault(date) < MaxPerDay)
        {
            return;
        }

        for (var next = date.AddDays(1); next <= searchEnd; next = next.AddDays(1))
        {
            if (booked.GetValueOrDefault(next) < MaxPerDay)
            {
                throw new DueDateFullException(date, next);
            }
        }

        throw new DomainException(
            $"{date:yyyy-MM-dd} is fully booked, and so is every day in the {options.Value.SearchDays} days after it.");
    }

    /// <summary>Non-cancelled orders per due date in [from, to].</summary>
    public Task<CapacityScheduleResponse> GetScheduleAsync(CancellationToken ct) => throw new NotImplementedException("Not yet test-driven.");

    public Task<CapacityScheduleResponse> PatchScheduleAsync(Microsoft.AspNetCore.JsonPatch.SystemTextJson.JsonPatchDocument<CapacitySchedulePatch> patch, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<IReadOnlyList<CapacityOverrideResponse>> GetOverridesAsync(CapacityOverrideQuery query, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<(CapacityOverrideResponse Override, bool Created)> SetOverrideAsync(DateOnly date, SetCapacityOverrideRequest request, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task RemoveOverrideAsync(DateOnly date, CancellationToken ct) => throw new NotImplementedException("Not yet test-driven.");

    private async Task<Dictionary<DateOnly, int>> CountPerDayAsync(DateOnly from, DateOnly to, Guid? ignoreOrderId, CancellationToken ct) =>
        await db.MaintenanceOrders
            .Where(o => o.DueDate >= from && o.DueDate <= to && o.Status != OrderStatus.Cancelled && o.Id != ignoreOrderId)
            .GroupBy(o => o.DueDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);
}
