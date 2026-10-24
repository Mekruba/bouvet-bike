using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Orders.Application;

/// <summary>When a customer registers, links the guest orders booked with the same email to the new account.</summary>
public sealed class GuestOrderLinker(AppDbContext db, TimeProvider clock) : ICustomerAccountEvents
{
    public async Task CustomerRegisteredAsync(Guid customerId, string email, CancellationToken ct)
    {
        var normalized = Email.Create(email);
        var guestOrders = await db.MaintenanceOrders
            .Where(o => o.CustomerId == null && o.Contact.Email == normalized)
            .ToListAsync(ct);

        foreach (var order in guestOrders)
        {
            order.LinkToCustomer(customerId, clock);
        }

        await db.SaveChangesAsync(ct);
    }
}
