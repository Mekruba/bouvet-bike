using BouvetBike.Orders.Application;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BouvetBike.Orders.Persistence;

/// <summary>Creates the weekday schedule at startup, from <c>Capacity:MaxOrdersPerDay</c>, if there isn't one.</summary>
public static class CapacitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        if (!await db.CapacitySchedules.AnyAsync())
        {
            db.CapacitySchedules.Add(CapacitySchedule.Create(services.GetRequiredService<IOptions<CapacityOptions>>().Value.MaxOrdersPerDay));
            await db.SaveChangesAsync();
        }
    }
}
