using BouvetBike.Shared.Contracts;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Orders.Contracts;

/// <summary>Staff order management. All methods throw <see cref="Shared.Application.NotFoundException"/> for an unknown ID.</summary>
public interface IAdminOrderService
{
    Task<PagedResponse<AdminOrderSummaryResponse>> SearchAsync(AdminOrderQuery query, CancellationToken ct);

    Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// JSON Patch on <see cref="AdminOrderPatch"/>. Re-checks capacity if the due date changes;
    /// setting <c>/discountCode</c> redeems it. Atomic: if anything fails, nothing changes.
    /// </summary>
    Task<AdminOrderResponse> PatchAsync(Guid id, JsonPatchDocument<AdminOrderPatch> patch, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Records a status change, by the current staff user, in the order's history.</summary>
    /// <exception cref="Shared.Domain.DomainException">The transition isn't allowed from the current status.</exception>
    Task<StatusChangeResponse> AddStatusChangeAsync(Guid id, ChangeStatusRequest request, CancellationToken ct);

    /// <summary>The order's status history, oldest first.</summary>
    Task<IReadOnlyList<StatusChangeResponse>> GetStatusChangesAsync(Guid id, CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">Unknown order or status change.</exception>
    Task<StatusChangeResponse> GetStatusChangeAsync(Guid id, Guid changeId, CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">Unknown order, or no override set.</exception>
    Task<PriceOverrideResponse> GetPriceOverrideAsync(Guid id, CancellationToken ct);

    /// <summary>Sets or replaces the final price, by the current staff user.</summary>
    /// <returns>The override, and whether it was created (no override before) rather than replaced (RFC 9110: 201 vs 200).</returns>
    /// <exception cref="Shared.Domain.DomainException">The order is cancelled.</exception>
    Task<(PriceOverrideResponse Override, bool Created)> SetPriceOverrideAsync(Guid id, OverridePriceRequest request, CancellationToken ct);

    /// <summary>Goes back to the calculated price. Idempotent: no override is not an error.</summary>
    /// <exception cref="Shared.Domain.DomainException">The order is cancelled.</exception>
    Task ClearPriceOverrideAsync(Guid id, CancellationToken ct);
}
