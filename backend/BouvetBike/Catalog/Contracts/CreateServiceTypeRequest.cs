using System.ComponentModel.DataAnnotations;
using BouvetBike.Catalog.Domain;

namespace BouvetBike.Catalog.Contracts;

/// <summary>A new service type for the catalog (Admin).</summary>
/// <param name="Price">In NOK.</param>
public sealed record CreateServiceTypeRequest(
    [Required, StringLength(ServiceType.NameMaxLength)] string Name,
    [StringLength(ServiceType.DescriptionMaxLength)] string? Description,
    [Required, Range(0, ServiceTypePatch.MaxPrice)] decimal? Price,
    [Required, Range(1, ServiceTypePatch.MaxMinutes)] int? EstimatedMinutes);
