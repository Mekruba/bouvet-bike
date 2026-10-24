using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Orders.Contracts;

/// <summary>Who to contact about an order. Email is lower case and phone is in E.164 format (e.g. <c>+4791234567</c>).</summary>
public sealed record ContactResponse(string Name, string Email, string Phone);

/// <summary>Price breakdown. Also used for quotes before ordering (never overridden then).</summary>
/// <param name="OverrideReason">Why staff set the final price. Shown to the customer, so they can see why it changed.</param>
public sealed record OrderPriceResponse(
    MoneyResponse ServicePrice,
    string? DiscountCode,
    MoneyResponse DiscountAmount,
    MoneyResponse Total,
    bool IsOverridden,
    string? OverrideReason = null);

/// <summary>What a customer sees: no internal notes, no staff details. Also the confirmation after ordering.</summary>
/// <param name="CanEditNotes">True while the order is New, the only time the customer may change their notes.</param>
public sealed record CustomerOrderResponse(
    string Reference,
    OrderStatus Status,
    ContactResponse Contact,
    string BikeBrand,
    string? BikeModel,
    string ServiceName,
    DateOnly DueDate,
    string? Notes,
    bool CanEditNotes,
    OrderPriceResponse Price,
    DateTime CreatedAt);

/// <summary>One row in a customer's order list: their account's orders, or the orders found by email or phone.</summary>
public sealed record CustomerOrderSummaryResponse(
    string Reference,
    OrderStatus Status,
    string BikeBrand,
    string ServiceName,
    DateOnly DueDate,
    MoneyResponse Total);
