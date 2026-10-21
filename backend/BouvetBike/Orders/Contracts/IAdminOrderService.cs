using BouvetBike.Shared.Contracts;

namespace BouvetBike.Orders.Contracts;

/// <summary>Staff order management. All methods throw <see cref="Shared.Application.NotFoundException"/> for an unknown ID.</summary>
public interface IAdminOrderService
{
    Task<PagedResponse<AdminOrderSummaryResponse>> SearchAsync(AdminOrderQuery query, CancellationToken ct);

    Task<AdminOrderResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Updates details and service. Re-checks capacity if the due date changes.</summary>
    Task<AdminOrderResponse> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Records the change, with the current staff user, in the status history.</summary>
    Task<AdminOrderResponse> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct);

    Task<AdminOrderResponse> UpdateInternalNotesAsync(Guid id, UpdateInternalNotesRequest request, CancellationToken ct);

    Task<AdminOrderResponse> ApplyDiscountAsync(Guid id, ApplyDiscountRequest request, CancellationToken ct);

    Task<AdminOrderResponse> RemoveDiscountAsync(Guid id, CancellationToken ct);

    Task<AdminOrderResponse> OverridePriceAsync(Guid id, OverridePriceRequest request, CancellationToken ct);

    Task<AdminOrderResponse> ClearPriceOverrideAsync(Guid id, CancellationToken ct);
}
