using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Mapping lives in IEntityTypeConfiguration<T> classes here in Infrastructure,
        // so domain types stay free of EF attributes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
