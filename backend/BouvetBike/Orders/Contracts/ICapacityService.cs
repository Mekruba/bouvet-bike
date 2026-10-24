using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Orders.Contracts;

/// <summary>
/// Daily capacity: how many non-cancelled orders a due date can take. A date's capacity is its override if it
/// has one, otherwise its weekday default from the schedule. Lives outside the order aggregate because it spans
/// many orders.
/// </summary>
public interface ICapacityService
{
    /// <summary>Bookings and capacity per date, starting at <see cref="CapacityQuery.From"/> (default today).</summary>
    Task<IReadOnlyList<DayCapacityResponse>> GetAsync(CapacityQuery query, CancellationToken ct);

    /// <summary>Checks that the date can take one more order, before an order is placed on it or moved to it.</summary>
    /// <param name="ignoreOrderId">An order being moved to a new date, so it doesn't count against itself.</param>
    /// <exception cref="Domain.DueDateFullException">The date is full or closed; carries the next available date.</exception>
    Task EnsureAvailableAsync(DateOnly date, Guid? ignoreOrderId, CancellationToken ct);

    /// <summary>The default capacity for each weekday.</summary>
    Task<CapacityScheduleResponse> GetScheduleAsync(CancellationToken ct);

    /// <summary>JSON Patch on the weekday defaults, atomically.</summary>
    /// <exception cref="Shared.Application.ConflictException">A <c>test</c> operation failed.</exception>
    /// <exception cref="Shared.Application.UnprocessableException">Unknown path or an out-of-range value.</exception>
    Task<CapacityScheduleResponse> PatchScheduleAsync(JsonPatchDocument<CapacitySchedulePatch> patch, CancellationToken ct);

    /// <summary>Date overrides in a range, by date.</summary>
    Task<IReadOnlyList<CapacityOverrideResponse>> GetOverridesAsync(CapacityOverrideQuery query, CancellationToken ct);

    /// <summary>Sets or replaces the capacity of one date.</summary>
    /// <returns>The override, and whether it was created rather than replaced (RFC 9110: 201 vs 200).</returns>
    /// <exception cref="Shared.Domain.DomainException">The date is in the past, or the capacity is out of range.</exception>
    Task<(CapacityOverrideResponse Override, bool Created)> SetOverrideAsync(DateOnly date, SetCapacityOverrideRequest request, CancellationToken ct);

    /// <summary>Back to the weekday default. Idempotent.</summary>
    Task RemoveOverrideAsync(DateOnly date, CancellationToken ct);
}
