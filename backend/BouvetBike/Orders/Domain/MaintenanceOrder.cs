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

    /// <summary>
    /// A new order with status New and a fresh reference. The service's name and price are copied onto it.
    /// The caller checks the date's capacity, which spans other orders.
    /// </summary>
    /// <param name="customerId">The logged-in customer placing it; null for a guest.</param>
    /// <exception cref="DomainException">
    /// Missing or too long fields, a retired service, a due date in the past, or one sooner than the service
    /// can be done (see <see cref="EarliestDueDate"/>; the details carry <c>earliestDueDate</c>).
    /// </exception>
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

        // Only when placing: staff edits later may set an earlier date, since they know when the bike came in.
        var earliest = EarliestDueDate(service, clock);
        if (dueDate < earliest)
        {
            var days = (int)service.EstimatedDuration.TotalDays;
            throw new DomainException(
                $"{service.Name} takes {(days == 1 ? "a day" : $"{days} days")}, so the earliest due date is {earliest:yyyy-MM-dd}.")
            {
                Details = new Dictionary<string, object?> { ["earliestDueDate"] = earliest },
            };
        }

        return order;
    }

    /// <summary>
    /// The first day an order for the service can be due: today plus the whole days the service takes.
    /// A service shorter than a day can be due today; one taking 24 hours, tomorrow at the earliest.
    /// </summary>
    public static DateOnly EarliestDueDate(ServiceType service, TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetLocalNow().Date).AddDays((int)service.EstimatedDuration.TotalDays);

    /// <summary>Staff edit of the order details. Not allowed once the order is completed or cancelled.</summary>
    /// <exception cref="StateConflictException">The order is completed or cancelled.</exception>
    /// <exception cref="DomainException">Missing or too long fields, or a new due date in the past.</exception>
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
    /// <exception cref="StateConflictException">The order is completed or cancelled.</exception>
    /// <exception cref="DomainException">The new service is retired.</exception>
    public void ChangeService(ServiceType service, TimeProvider clock)
    {
        EnsureNotClosed();
        if (service.Id != ServiceTypeId)
        {
            SetService(service);
            Touch(clock);
        }
    }

    /// <summary>
    /// Links a guest order to a customer account (when the customer confirms the order's email).
    /// Linking to the same account again does nothing.
    /// </summary>
    /// <exception cref="StateConflictException">The order already belongs to another account.</exception>
    public void LinkToCustomer(Guid customerId, TimeProvider clock)
    {
        Guard.NotEmpty(customerId, "Customer");
        if (CustomerId == customerId)
        {
            return;
        }

        if (CustomerId is not null)
        {
            throw new StateConflictException("The order already belongs to another account.");
        }

        CustomerId = customerId;
        Touch(clock);
    }

    /// <summary>Moves the order to a new status and records who, when and why in <see cref="StatusHistory"/>.</summary>
    /// <param name="changedById">The staff member making the change.</param>
    /// <exception cref="StateConflictException">The move isn't allowed from the current status (see <see cref="OrderStatusTransitions"/>).</exception>
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
    /// <exception cref="StateConflictException">The status is no longer New.</exception>
    /// <exception cref="DomainException">Longer than <see cref="NotesMaxLength"/>.</exception>
    public void UpdateCustomerNotes(string? notes, TimeProvider clock)
    {
        if (Status != OrderStatus.New)
        {
            throw new StateConflictException("Notes can only be changed before the repair has started.");
        }

        CustomerNotes = Guard.Optional(notes, "Notes", NotesMaxLength);
        Touch(clock);
    }

    /// <summary>Staff notes, allowed at any status. Empty clears them.</summary>
    /// <exception cref="DomainException">Longer than <see cref="NotesMaxLength"/>.</exception>
    public void UpdateInternalNotes(string? notes, TimeProvider clock)
    {
        InternalNotes = Guard.Optional(notes, "Internal notes", NotesMaxLength);
        Touch(clock);
    }

    /// <summary>Applies terms from <see cref="DiscountCode.Redeem"/>. One discount code per order.</summary>
    /// <exception cref="StateConflictException">The order is completed or cancelled, or already has a code.</exception>
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

    /// <summary>Removes the discount code. The use it counted on the code isn't given back.</summary>
    /// <exception cref="StateConflictException">The order is completed or cancelled.</exception>
    public void RemoveDiscount(TimeProvider clock)
    {
        EnsureNotClosed();
        Discount = null;
        Touch(clock);
    }

    /// <summary>Sets the final price. Allowed on completed orders (that's when the final price is known), not on cancelled ones.</summary>
    /// <param name="reason">Shown to the customer next to the price.</param>
    /// <exception cref="StateConflictException">The order is cancelled.</exception>
    public void OverridePrice(Money amount, string reason, Guid staffId, TimeProvider clock)
    {
        EnsureNotCancelled();
        var now = clock.GetUtcNow().UtcDateTime;
        PriceOverride = new PriceOverride(amount, reason, staffId, now);
        UpdatedAt = now;
    }

    /// <summary>Goes back to the calculated price. Also fine when no override is set.</summary>
    /// <exception cref="StateConflictException">The order is cancelled.</exception>
    public void ClearPriceOverride(TimeProvider clock)
    {
        EnsureNotCancelled();
        PriceOverride = null;
        Touch(clock);
    }

    /// <summary>Copies the service's ID, name and current price onto the order.</summary>
    /// <exception cref="DomainException">The service is retired.</exception>
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

    /// <summary>Sets the due date; today or later, in the shop's local time.</summary>
    /// <exception cref="DomainException">The date is in the past.</exception>
    private void SetDueDate(DateOnly dueDate, TimeProvider clock)
    {
        if (dueDate < DateOnly.FromDateTime(clock.GetLocalNow().Date))
        {
            throw new DomainException("Due date cannot be in the past.");
        }

        DueDate = dueDate;
    }

    /// <summary>Completed and cancelled orders are final: their details, service and discount can't change.</summary>
    private void EnsureNotClosed()
    {
        if (Status.IsTerminal())
        {
            throw new StateConflictException($"A {Status.ToString().ToLowerInvariant()} order cannot be changed.");
        }
    }

    /// <summary>For changes still allowed on completed orders, such as the final price.</summary>
    private void EnsureNotCancelled()
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new StateConflictException("A cancelled order cannot be changed.");
        }
    }

    /// <summary>Records that the order changed now.</summary>
    private void Touch(TimeProvider clock) => UpdatedAt = clock.GetUtcNow().UtcDateTime;
}
