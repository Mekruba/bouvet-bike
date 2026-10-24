using BouvetBike.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Catalog.Persistence;

/// <summary>Table mapping for <see cref="ServiceType"/>.</summary>
public class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    /// <summary>Unique, case-insensitive names; the price stored inline as a complex type.</summary>
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(ServiceType.NameMaxLength).UseCollation("NOCASE");
        builder.HasIndex(s => s.Name).IsUnique(); // case-insensitive through the collation
        builder.Property(s => s.Description).HasMaxLength(ServiceType.DescriptionMaxLength);
        builder.ComplexProperty(s => s.BasePrice);
    }
}
