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

    internal PriceOverride(Money amount, string reason, Guid overriddenById, DateTime overriddenAt)
    {
        Amount = amount;
        Reason = Guard.Required(reason, "Reason", ReasonMaxLength);
        OverriddenById = Guard.NotEmpty(overriddenById, "Staff user");
        OverriddenAt = overriddenAt;
    }
}
