using BouvetBike.Catalog.Domain;
using BouvetBike.Discounts.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Shared.Persistence;

/// <summary>One context for the whole app (Identity + all modules). Each module maps its own types in its Persistence/ folder.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>().HaveMaxLength(Email.MaxLength);
        configurationBuilder.Properties<PhoneNumber>().HaveConversion<PhoneNumberConverter>().HaveMaxLength(PhoneNumber.MaxLength);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables

        // Picks up every module's IEntityTypeConfiguration<T>, so domain types stay free of EF attributes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Domain entities create their own IDs. Without this, EF treats a new child entity
        // (e.g. a status change added to a loaded order) as existing, because its key is already set.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.ClrType.IsAssignableTo(typeof(Entity))))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }
}
