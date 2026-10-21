using BouvetBike.Catalog.Domain;
using BouvetBike.Discounts.Domain;
using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>
/// A bike maintenance order: the aggregate root for its status history.
/// Daily capacity is checked outside the order, since it spans many orders.
/// </summary>
public sealed class MaintenanceOrder : AggregateRoot
{
    public const int BikeBrandMaxLength = 100;
    public const int BikeModelMaxLength = 100;
    public const int NotesMaxLength = 2000;

    private readonly List<OrderStatusChange> _statusHistory = [];

    public OrderReference Reference { get; private set; } = null!;
    public ContactInfo Contact { get; private set; } = null!;

    /// <summary>The registered customer who placed the order; null for guest orders.</summary>
    public Guid? CustomerId { get; private set; }

    public string BikeBrand { get; private set; } = null!;
    public string? BikeModel { get; private set; }

    /// <summary>The ordered service. Name and price are copied from the catalog so later catalog changes don't alter the order.</summary>
    public Guid ServiceTypeId { get; private set; }
    public string ServiceName { get; private set; } = null!;
    public Money ServicePrice { get; private set; } = null!;

    public DateOnly DueDate { get; private set; }
    public OrderStatus Status { get; private set; }

    /// <summary>Written by the customer; editable until the repair has started.</summary>
    public string? CustomerNotes { get; private set; }

    /// <summary>Written by staff; never shown to the customer.</summary>
    public string? InternalNotes { get; private set; }

    public AppliedDiscount? Discount { get; private set; }
    public PriceOverride? PriceOverride { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<OrderStatusChange> StatusHistory => _statusHistory;

    public Money DiscountAmount => Discount?.CalculateFor(ServicePrice) ?? Money.Zero();

    /// <summary>The price the customer pays: the staff override if set, otherwise service price minus discount.</summary>
    public Money Total => PriceOverride?.Amount ?? ServicePrice - DiscountAmount;

    private MaintenanceOrder() { } // EF Core

    public static MaintenanceOrder Place(
        ContactInfo contact,
        string bikeBrand,
        string? bikeModel,
        ServiceType service,
        DateOnly dueDate,
        TimeProvider clock,
        Guid? customerId = null,
        string? customerNotes = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var order = new MaintenanceOrder
        {
            Reference = OrderReference.New(),
            Contact = contact,
            CustomerId = customerId,
            BikeBrand = Guard.Required(bikeBrand, "Bike brand", BikeBrandMaxLength),
            BikeModel = Guard.Optional(bikeModel, "Bike model", BikeModelMaxLength),
            CustomerNotes = Guard.Optional(customerNotes, "Notes", NotesMaxLength),
            Status = OrderStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
        };

        order.SetService(service);
        order.SetDueDate(dueDate, clock);
        return order;
    }

    /// <summary>Staff edit of the order details. Not allowed once the order is completed or cancelled.</summary>
    public void UpdateDetails(ContactInfo contact, string bikeBrand, string? bikeModel, DateOnly dueDate, TimeProvider clock)
    {
        EnsureNotClosed();
        var brand = Guard.Required(bikeBrand, "Bike brand", BikeBrandMaxLength);
        var model = Guard.Optional(bikeModel, "Bike model", BikeModelMaxLength);
        if (dueDate != DueDate)
        {
            // Only a changed date is checked, so staff can still edit an order whose date has passed.
            SetDueDate(dueDate, clock);
        }

        Contact = contact;
        BikeBrand = brand;
        BikeModel = model;
        Touch(clock);
    }

    /// <summary>Switches to another service, priced from the catalog now. Choosing the current service again keeps its original price.</summary>
    public void ChangeService(ServiceType service, TimeProvider clock)
    {
        EnsureNotClosed();
        if (service.Id != ServiceTypeId)
        {
            SetService(service);
            Touch(clock);
        }
    }

    public void ChangeStatus(OrderStatus newStatus, Guid? changedById, TimeProvider clock, string? reason = null)
    {
        if (!OrderStatusTransitions.CanTransition(Status, newStatus))
        {
            throw new StateConflictException($"Cannot change status from {Status} to {newStatus}.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        _statusHistory.Add(new OrderStatusChange(Status, newStatus, changedById, now, reason));
        Status = newStatus;
        UpdatedAt = now;
    }

    /// <summary>Customers may add or change their notes until the repair has started. Empty clears them.</summary>
    public void UpdateCustomerNotes(string? notes, TimeProvider clock)
    {
        if (Status != OrderStatus.New)
        {
            throw new StateConflictException("Notes can only be changed before the repair has started.");
        }

        CustomerNotes = Guard.Optional(notes, "Notes", NotesMaxLength);
        Touch(clock);
    }

    /// <summary>Staff notes, allowed at any status.</summary>
    public void UpdateInternalNotes(string? notes, TimeProvider clock)
    {
        InternalNotes = Guard.Optional(notes, "Internal notes", NotesMaxLength);
        Touch(clock);
    }

    /// <summary>Applies terms from <see cref="DiscountCode.Redeem"/>. One discount code per order.</summary>
    public void ApplyDiscount(AppliedDiscount discount, TimeProvider clock)
    {
        EnsureNotClosed();
        if (Discount is not null)
        {
            throw new StateConflictException($"Discount code '{Discount.Code}' is already applied.");
        }

        Discount = discount;
        Touch(clock);
    }

    public void RemoveDiscount(TimeProvider clock)
    {
        EnsureNotClosed();
        Discount = null;
        Touch(clock);
    }

    /// <summary>Sets the final price. Allowed on completed orders (that's when the final price is known), not on cancelled ones.</summary>
    public void OverridePrice(Money amount, string reason, Guid staffId, TimeProvider clock)
    {
        EnsureNotCancelled();
        var now = clock.GetUtcNow().UtcDateTime;
        PriceOverride = new PriceOverride(amount, reason, staffId, now);
        UpdatedAt = now;
    }

    public void ClearPriceOverride(TimeProvider clock)
    {
        EnsureNotCancelled();
        PriceOverride = null;
        Touch(clock);
    }

    private void SetService(ServiceType service)
    {
        if (!service.IsActive)
        {
            throw new DomainException($"Service '{service.Name}' is not available.");
        }

        ServiceTypeId = service.Id;
        ServiceName = service.Name;
        ServicePrice = service.BasePrice;
    }

    private void SetDueDate(DateOnly dueDate, TimeProvider clock)
    {
        if (dueDate < DateOnly.FromDateTime(clock.GetLocalNow().Date))
        {
            throw new DomainException("Due date cannot be in the past.");
        }

        DueDate = dueDate;
    }

    private void EnsureNotClosed()
    {
        if (Status.IsTerminal())
        {
            throw new StateConflictException($"A {Status.ToString().ToLowerInvariant()} order cannot be changed.");
        }
    }

    private void EnsureNotCancelled()
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new StateConflictException("A cancelled order cannot be changed.");
        }
    }

    private void Touch(TimeProvider clock) => UpdatedAt = clock.GetUtcNow().UtcDateTime;
}
