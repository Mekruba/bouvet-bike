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

    Task<IReadOnlyList<StatusChangeResponse>> GetStatusChangesAsync(Guid id, CancellationToken ct);

    Task<AdminOrderResponse> SetPriceOverrideAsync(Guid id, OverridePriceRequest request, CancellationToken ct);

    Task ClearPriceOverrideAsync(Guid id, CancellationToken ct);
}
