using BouvetBike.Discounts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Discounts.Persistence;

/// <summary>Table mapping for <see cref="DiscountCode"/>.</summary>
public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    /// <summary>Unique codes. They're stored upper case, so a plain unique index is case-insensitive in practice.</summary>
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.Property(d => d.Code).HasMaxLength(DiscountCode.CodeMaxLength);
        builder.HasIndex(d => d.Code).IsUnique();
    }
}
