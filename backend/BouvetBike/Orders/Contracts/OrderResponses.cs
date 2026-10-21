using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Orders.Contracts;

public sealed record ContactResponse(string Name, string Email, string Phone);

/// <summary>Price breakdown. Also used for quotes before ordering (never overridden then).</summary>
public sealed record OrderPriceResponse(
    MoneyResponse ServicePrice,
    string? DiscountCode,
    MoneyResponse DiscountAmount,
    MoneyResponse Total,
    bool IsOverridden);

/// <summary>What a customer sees: no internal notes, no staff details. Also the confirmation after ordering.</summary>
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

/// <summary>One row in a logged-in customer's order list.</summary>
public sealed record CustomerOrderSummaryResponse(
    string Reference,
    OrderStatus Status,
    string BikeBrand,
    string ServiceName,
    DateOnly DueDate,
    MoneyResponse Total);
