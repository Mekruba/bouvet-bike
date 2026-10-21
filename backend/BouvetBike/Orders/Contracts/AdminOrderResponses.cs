using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Orders.Contracts;

/// <summary>One row in the admin order list.</summary>
public sealed record AdminOrderSummaryResponse(
    Guid Id,
    string Reference,
    OrderStatus Status,
    ContactResponse Contact,
    bool IsGuest,
    string BikeBrand,
    string ServiceName,
    DateOnly DueDate,
    MoneyResponse Total,
    DateTime CreatedAt);

/// <summary>Full order for staff, including internal notes, price override and status history.</summary>
/// <param name="AllowedStatusChanges">Valid next statuses, so the UI only offers legal transitions.</param>
public sealed record AdminOrderResponse(
    Guid Id,
    string Reference,
    OrderStatus Status,
    IReadOnlyList<OrderStatus> AllowedStatusChanges,
    ContactResponse Contact,
    Guid? CustomerId,
    string BikeBrand,
    string? BikeModel,
    Guid ServiceTypeId,
    string ServiceName,
    DateOnly DueDate,
    string? CustomerNotes,
    string? InternalNotes,
    OrderPriceResponse Price,
    PriceOverrideResponse? PriceOverride,
    IReadOnlyList<StatusChangeResponse> StatusHistory,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PriceOverrideResponse(MoneyResponse Amount, string Reason, string? OverriddenBy, DateTime OverriddenAt);

/// <param name="ChangedBy">Staff member's name; null for system or guest changes.</param>
public sealed record StatusChangeResponse(OrderStatus From, OrderStatus To, string? ChangedBy, DateTime ChangedAt, string? Reason);
