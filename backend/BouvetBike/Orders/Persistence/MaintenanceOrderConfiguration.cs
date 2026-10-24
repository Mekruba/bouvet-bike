using BouvetBike.Catalog.Domain;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BouvetBike.Orders.Persistence;

/// <summary>Table mapping for <see cref="MaintenanceOrder"/>.</summary>
public class MaintenanceOrderConfiguration : IEntityTypeConfiguration<MaintenanceOrder>
{
    /// <summary>
    /// Value objects stored inline (contact, prices, discount and override as complex types), a unique reference,
    /// indexes for the capacity count and status filter, and the status history as a child table loaded with the order.
    /// </summary>
    public void Configure(EntityTypeBuilder<MaintenanceOrder> builder)
    {
        builder.Property(o => o.Reference)
            .HasConversion(v => v.Value, v => OrderReference.Parse(v))
            .HasMaxLength(OrderReference.MaxLength);
        builder.HasIndex(o => o.Reference).IsUnique();
        builder.HasIndex(o => o.DueDate); // daily capacity counts
        builder.HasIndex(o => o.Status);

        builder.ComplexProperty(o => o.Contact, contact =>
        {
            contact.Property(c => c.Name).HasMaxLength(ContactInfo.NameMaxLength).HasColumnName("ContactName").UseCollation("NOCASE");
            contact.Property(c => c.Email).HasColumnName("ContactEmail");
            contact.Property(c => c.Phone).HasColumnName("ContactPhone");
        });

        builder.Property(o => o.BikeBrand).HasMaxLength(MaintenanceOrder.BikeBrandMaxLength);
        builder.Property(o => o.BikeModel).HasMaxLength(MaintenanceOrder.BikeModelMaxLength);
        builder.Property(o => o.ServiceName).HasMaxLength(ServiceType.NameMaxLength);
        builder.ComplexProperty(o => o.ServicePrice);
        builder.Property(o => o.CustomerNotes).HasMaxLength(MaintenanceOrder.NotesMaxLength);
        builder.Property(o => o.InternalNotes).HasMaxLength(MaintenanceOrder.NotesMaxLength);

        builder.ComplexProperty(o => o.Discount, discount =>
        {
            discount.Property(d => d.Code).HasMaxLength(DiscountCode.CodeMaxLength);
        });

        builder.ComplexProperty(o => o.PriceOverride, priceOverride =>
        {
            priceOverride.ComplexProperty(p => p.Amount);
            priceOverride.Property(p => p.Reason).HasMaxLength(PriceOverride.ReasonMaxLength);
        });

        // References to other aggregates are IDs only (no navigations), but still real foreign keys.
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(o => o.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);

        // Status history is part of the aggregate: loaded and deleted with the order.
        builder.HasMany(o => o.StatusHistory).WithOne().HasForeignKey("MaintenanceOrderId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.StatusHistory).AutoInclude();
    }
}

/// <summary>Table mapping for <see cref="OrderStatusChange"/>.</summary>
public class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    /// <summary>The staff reference is cleared, not the history entry, if the user is deleted.</summary>
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("OrderStatusChanges");
        builder.Property(c => c.Reason).HasMaxLength(OrderStatusChange.ReasonMaxLength);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.ChangedById).OnDelete(DeleteBehavior.SetNull);
    }
}
