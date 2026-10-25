using System.ComponentModel.DataAnnotations;
using BouvetBike.Catalog.Domain;

namespace BouvetBike.Catalog.Contracts;

/// <summary>
/// The patchable view of a service type: the target of a JSON Patch (RFC 6902) document.
/// Paths are the camelCase property names, e.g. <c>/price</c> or <c>/isActive</c>.
/// The patch is applied to this model, which is then validated and passed to the domain,
/// so a patch can never bypass the domain's rules. <c>id</c> is not patchable.
/// </summary>
public sealed class ServiceTypePatch
{
    public const int MaxPrice = 1_000_000;
    /// <summary>Two weeks: services can take several days (orders for them are due that many days ahead).</summary>
    public const int MaxMinutes = 14 * 24 * 60;

    [Required, StringLength(ServiceType.NameMaxLength)]
    public string Name { get; set; } = "";

    [StringLength(ServiceType.DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>In NOK.</summary>
    [Range(0, MaxPrice)]
    public decimal Price { get; set; }

    [Range(1, MaxMinutes)]
    public int EstimatedMinutes { get; set; }

    public bool IsActive { get; set; }
}
