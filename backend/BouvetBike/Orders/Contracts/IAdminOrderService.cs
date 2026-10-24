using BouvetBike.Shared.Contracts;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Orders.Contracts;

/// <summary>Staff order management. All methods throw <see cref="Shared.Application.NotFoundException"/> for an unknown ID.</summary>
public interface IAdminOrderService
{
    /// <summary>One page of orders, filtered, searched and sorted as the query asks. Doesn't throw for an empty result.</summary>
    Task<PagedResponse<AdminOrderSummaryResponse>> SearchAsync(AdminOrderQuery query, CancellationToken ct);

    /// <summary>The full order: internal notes, price override, status history and the status moves allowed now.</summary>
    Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// JSON Patch on <see cref="AdminOrderPatch"/>. Re-checks capacity if the due date changes;
    /// setting <c>/discountCode</c> redeems it. Atomic: if anything fails, nothing changes.
    /// </summary>
    /// <exception cref="Shared.Domain.StateConflictException">The order's status forbids the change, e.g. details of a completed order.</exception>
    /// <exception cref="Shared.Application.ConflictException">A <c>test</c> operation failed.</exception>
    /// <exception cref="Shared.Application.UnprocessableException">Unknown path, invalid values, a full due date or an unusable code.</exception>
    Task<AdminOrderResponse> PatchAsync(Guid id, JsonPatchDocument<AdminOrderPatch> patch, CancellationToken ct);

    /// <summary>Deletes the order and its history for good.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Records a status change, by the current staff user, in the order's history.</summary>
    /// <exception cref="Shared.Domain.StateConflictException">The transition isn't allowed from the current status.</exception>
    Task<StatusChangeResponse> AddStatusChangeAsync(Guid id, ChangeStatusRequest request, CancellationToken ct);

    /// <summary>The order's status history, oldest first.</summary>
    Task<IReadOnlyList<StatusChangeResponse>> GetStatusChangesAsync(Guid id, CancellationToken ct);

    /// <summary>One entry in the order's status history.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown order or status change.</exception>
    Task<StatusChangeResponse> GetStatusChangeAsync(Guid id, Guid changeId, CancellationToken ct);

    /// <summary>The final price staff set, with who set it, when and why.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown order, or no override set.</exception>
    Task<PriceOverrideResponse> GetPriceOverrideAsync(Guid id, CancellationToken ct);

    /// <summary>Sets or replaces the final price, by the current staff user.</summary>
    /// <returns>The override, and whether it was created (no override before) rather than replaced (RFC 9110: 201 vs 200).</returns>
    /// <exception cref="Shared.Domain.StateConflictException">The order is cancelled.</exception>
    Task<(PriceOverrideResponse Override, bool Created)> SetPriceOverrideAsync(Guid id, OverridePriceRequest request, CancellationToken ct);

    /// <summary>Goes back to the calculated price. Idempotent: no override is not an error.</summary>
    /// <exception cref="Shared.Domain.StateConflictException">The order is cancelled.</exception>
    Task ClearPriceOverrideAsync(Guid id, CancellationToken ct);
}
