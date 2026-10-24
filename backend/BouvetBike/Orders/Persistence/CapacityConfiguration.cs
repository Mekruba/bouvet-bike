using BouvetBike.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Orders.Persistence;

public class CapacityScheduleConfiguration : IEntityTypeConfiguration<CapacitySchedule>
{
    public void Configure(EntityTypeBuilder<CapacitySchedule> builder) => builder.ToTable("CapacitySchedules");
}

public class CapacityOverrideConfiguration : IEntityTypeConfiguration<CapacityOverride>
{
    public void Configure(EntityTypeBuilder<CapacityOverride> builder)
    {
        builder.HasIndex(o => o.Date).IsUnique();
        builder.Property(o => o.Reason).HasMaxLength(CapacityOverride.ReasonMaxLength);
    }
}
