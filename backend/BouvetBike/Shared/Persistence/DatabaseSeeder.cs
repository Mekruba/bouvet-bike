using BouvetBike.Catalog.Domain;
using BouvetBike.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Shared.Persistence;

/// <summary>Inserts reference data when the database is created.</summary>
public static class DatabaseSeeder
{
    /// <summary>Adds the service catalog if it is empty. Called by EF Core (<c>UseSeeding</c>) from <c>EnsureCreated</c>.</summary>
    public static void Seed(DbContext db, bool _)
    {
        if (!db.Set<ServiceType>().Any())
        {
            db.Set<ServiceType>().AddRange(CatalogSeed.ServiceTypes());
            db.SaveChanges();
        }
    }

    /// <summary>The async twin of <see cref="Seed"/>, used by <c>EnsureCreatedAsync</c> (<c>UseAsyncSeeding</c>).</summary>
    public static async Task SeedAsync(DbContext db, bool _, CancellationToken ct)
    {
        if (!await db.Set<ServiceType>().AnyAsync(ct))
        {
            db.Set<ServiceType>().AddRange(CatalogSeed.ServiceTypes());
            await db.SaveChangesAsync(ct);
        }
    }
}
