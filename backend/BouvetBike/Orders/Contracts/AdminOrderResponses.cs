using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Orders.Contracts;

/// <summary>One row in the admin order list.</summary>
/// <param name="IsGuest">True when the order isn't linked to a customer account.</param>
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

/// <summary>A final price set by an admin.</summary>
/// <param name="Reason">Also shown to the customer.</param>
/// <param name="OverriddenBy">The admin's name; null if the account was deleted.</param>
public sealed record PriceOverrideResponse(MoneyResponse Amount, string Reason, string? OverriddenBy, DateTime OverriddenAt);

/// <summary>One step in an order's status history.</summary>
/// <param name="ChangedBy">Staff member's name; null for system or guest changes.</param>
public sealed record StatusChangeResponse(Guid Id, OrderStatus From, OrderStatus To, string? ChangedBy, DateTime ChangedAt, string? Reason);
