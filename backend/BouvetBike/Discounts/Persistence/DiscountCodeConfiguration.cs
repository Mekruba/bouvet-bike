using BouvetBike.Discounts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Discounts.Persistence;

public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.Property(d => d.Code).HasMaxLength(DiscountCode.CodeMaxLength);
        builder.HasIndex(d => d.Code).IsUnique();
    }
}
