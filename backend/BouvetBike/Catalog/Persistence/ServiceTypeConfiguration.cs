using BouvetBike.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Catalog.Persistence;

public class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(ServiceType.NameMaxLength).UseCollation("NOCASE");
        builder.HasIndex(s => s.Name).IsUnique(); // case-insensitive through the collation
        builder.Property(s => s.Description).HasMaxLength(ServiceType.DescriptionMaxLength);
        builder.ComplexProperty(s => s.BasePrice);
    }
}
