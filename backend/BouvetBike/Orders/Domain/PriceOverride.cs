using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>A final price set by staff, replacing the calculated total. Who, when and why are kept for audit.</summary>
public sealed record PriceOverride
{
    public const int ReasonMaxLength = 500;

    public Money Amount { get; private init; } = null!;
    public string Reason { get; private init; } = null!;
    public Guid OverriddenById { get; private init; }
    public DateTime OverriddenAt { get; private init; }

    private PriceOverride() { } // EF Core

    /// <summary>Created only by <see cref="MaintenanceOrder.OverridePrice"/>.</summary>
    /// <exception cref="DomainException">Missing or too long reason, or no staff user.</exception>
    internal PriceOverride(Money amount, string reason, Guid overriddenById, DateTime overriddenAt)
    {
        Amount = amount;
        Reason = Guard.Required(reason, "Reason", ReasonMaxLength);
        OverriddenById = Guard.NotEmpty(overriddenById, "Staff user");
        OverriddenAt = overriddenAt;
    }
}
