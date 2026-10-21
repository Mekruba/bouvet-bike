using System.ComponentModel.DataAnnotations;
using BouvetBike.Discounts.Domain;

namespace BouvetBike.Discounts.Contracts;

/// <summary>
/// What an admin may change on a discount code with JSON Patch. <c>/code</c> is not patchable: orders keep the
/// code they used, so renaming one would break that link. Retire a code with <c>/isActive</c> false.
/// </summary>
public sealed class DiscountCodePatch
{
    public DiscountType Type { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal Value { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Null for unlimited.</summary>
    [Range(1, int.MaxValue)]
    public int? MaxUses { get; set; }

    public bool IsActive { get; set; }
}
