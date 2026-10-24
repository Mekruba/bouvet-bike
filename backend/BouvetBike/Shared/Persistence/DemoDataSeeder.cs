using BouvetBike.Discounts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Shared.Persistence;

/// <summary>
/// Data for trying the app locally. Only called in the Development environment, so it never reaches a
/// production database, and tests (Testing environment) create their own data.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Adds the demo discount codes that don't exist yet, so it can run on every start.</summary>
    public static async Task SeedAsync(AppDbContext db)
    {
        foreach (var code in DiscountSeed.DemoCodes())
        {
            if (!await db.DiscountCodes.AnyAsync(d => d.Code == code.Code))
            {
                db.DiscountCodes.Add(code);
            }
        }

        await db.SaveChangesAsync();
    }
}
