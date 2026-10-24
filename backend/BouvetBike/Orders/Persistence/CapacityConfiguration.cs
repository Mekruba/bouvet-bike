using BouvetBike.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Orders.Persistence;

/// <summary>Table mapping for <see cref="CapacitySchedule"/>: one row, a column per weekday.</summary>
public class CapacityScheduleConfiguration : IEntityTypeConfiguration<CapacitySchedule>
{
    /// <summary>Maps to the <c>CapacitySchedules</c> table.</summary>
    public void Configure(EntityTypeBuilder<CapacitySchedule> builder) => builder.ToTable("CapacitySchedules");
}

/// <summary>Table mapping for <see cref="CapacityOverride"/>.</summary>
public class CapacityOverrideConfiguration : IEntityTypeConfiguration<CapacityOverride>
{
    /// <summary>At most one override per date.</summary>
    public void Configure(EntityTypeBuilder<CapacityOverride> builder)
    {
        builder.HasIndex(o => o.Date).IsUnique();
        builder.Property(o => o.Reason).HasMaxLength(CapacityOverride.ReasonMaxLength);
    }
}
