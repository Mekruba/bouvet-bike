using BouvetBike.Catalog.Domain;
using BouvetBike.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Shared.Persistence;

/// <summary>Inserts reference data when the database is created.</summary>
public static class DatabaseSeeder
{
    public static void Seed(DbContext db, bool _)
    {
        if (!db.Set<ServiceType>().Any())
        {
            db.Set<ServiceType>().AddRange(CatalogSeed.ServiceTypes());
            db.SaveChanges();
        }
    }

    public static async Task SeedAsync(DbContext db, bool _, CancellationToken ct)
    {
        if (!await db.Set<ServiceType>().AnyAsync(ct))
        {
            db.Set<ServiceType>().AddRange(CatalogSeed.ServiceTypes());
            await db.SaveChangesAsync(ct);
        }
    }
}
