using BouvetBike.Catalog.Domain;
using BouvetBike.Discounts.Domain;
using BouvetBike.Discounts.Persistence;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using BouvetBike.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BouvetBike.Shared.Persistence;

/// <summary>
/// Fills a new database with orders from the last three weeks and the next two, so the app looks like it's in use:
/// every status, history by named mechanics, notes, discounts, a price override, a full day, a closed day and a day
/// with extra capacity. Runs only when <c>SeedSampleData</c> is set and the database was just created.
/// </summary>
/// <remarks>
/// Orders go through the real domain methods, each step with a clock set to when it "happened", so every rule
/// applies. An order is only added while its due date has room, so the data stays valid whatever the capacity is.
/// The sample accounts (two mechanics, an admin and a customer) have no password, so they can't be used to log in.
/// </remarks>
public static class SampleDataSeeder
{
    private const string SampleCustomerEmail = "kari.nordmann@example.com";

    /// <summary>One sample order: who, what, when (in days from today), and how far it got.</summary>
    /// <param name="Placed">Days from today it was placed (0 or negative).</param>
    /// <param name="Due">Days from today it's due.</param>
    /// <param name="Steps">Status changes, each with the day (from today, not after today) it happened.</param>
    private sealed record SampleOrder(
        string Name,
        string Bike,
        string Service,
        int Placed,
        int Due,
        (OrderStatus Status, int Day, string? Reason)[] Steps,
        string? Notes = null,
        string? InternalNotes = null,
        string? DiscountCode = null,
        (decimal Amount, string Reason)? PriceOverride = null);

    /// <summary>Adds the sample accounts, capacity overrides and orders, and saves them in one go.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = services.GetRequiredService<TimeProvider>();
        var today = DateOnly.FromDateTime(clock.GetLocalNow().Date);

        var ola = await AddAccountAsync(users, "Ola Hansen", "ola.hansen@example.com", Roles.Mechanic);
        var ingrid = await AddAccountAsync(users, "Ingrid Berg", "ingrid.berg@example.com", Roles.Mechanic);
        var marte = await AddAccountAsync(users, "Marte Vik", "marte.vik@example.com", Roles.Admin);
        var customer = await AddAccountAsync(users, "Kari Nordmann", SampleCustomerEmail, Roles.Customer);

        var at = Clocks(clock);
        var overrides = new[]
        {
            CapacityOverride.Create(today.AddDays(3), 7, "Extra mechanic in for the spring rush", at(0, 9)),
            CapacityOverride.Create(today.AddDays(6), 0, "Staff training", at(0, 9)),
        };
        db.CapacityOverrides.AddRange(overrides);

        var catalog = await db.ServiceTypes.Where(s => s.IsActive).ToListAsync();
        var schedule = await db.CapacitySchedules.SingleAsync();
        var codes = await EnsureDiscountCodesAsync(db);
        var booked = new Dictionary<DateOnly, int>();
        var phones = new Dictionary<string, PhoneNumber>(); // one number per person, also across their orders

        int CapacityOn(DateOnly date) =>
            overrides.FirstOrDefault(o => o.Date == date)?.MaxOrders ?? schedule.ForDay(date.DayOfWeek);

        foreach (var (sample, index) in SampleOrders().Select((s, i) => (s, i)))
        {
            var dueDate = today.AddDays(sample.Due);
            var cancelled = sample.Steps.Any(s => s.Status == OrderStatus.Cancelled);
            if (!cancelled && booked.GetValueOrDefault(dueDate) >= CapacityOn(dueDate))
            {
                continue; // the day is full (e.g. a lower configured capacity); skip rather than overbook
            }

            var service = catalog.FirstOrDefault(s => s.Name == sample.Service) ?? catalog.First();
            var (brand, model) = SplitBike(sample.Bike);
            var email = sample.Name == "Kari Nordmann" ? SampleCustomerEmail : EmailFor(sample.Name);
            if (!phones.TryGetValue(sample.Name, out var phone))
            {
                phone = phones[sample.Name] = PhoneNumber.Create($"400 00 {phones.Count + 100:000}"); // obviously made up
            }

            var contact = new ContactInfo(sample.Name, Email.Create(email), phone);
            var placedAt = at(sample.Placed, 10 + index % 6);

            var order = MaintenanceOrder.Place(
                contact, brand, model, service, dueDate, placedAt,
                customerId: email == SampleCustomerEmail ? customer.Id : null, customerNotes: sample.Notes);

            if (sample.DiscountCode is { } code)
            {
                order.ApplyDiscount(codes[code].Redeem(placedAt), placedAt);
            }

            if (sample.InternalNotes is { } internalNotes)
            {
                order.UpdateInternalNotes(internalNotes, placedAt);
            }

            foreach (var (step, number) in sample.Steps.Select((s, n) => (s, n)))
            {
                var mechanic = number % 2 == 0 ? ola : ingrid;
                order.ChangeStatus(step.Status, mechanic.Id, at(step.Day, 12 + number), step.Reason);
            }

            if (sample.PriceOverride is { } priceOverride)
            {
                order.OverridePrice(new Money(priceOverride.Amount), priceOverride.Reason, marte.Id, at(sample.Steps[^1].Day, 16));
            }

            if (!cancelled)
            {
                booked[dueDate] = booked.GetValueOrDefault(dueDate) + 1;
            }

            db.MaintenanceOrders.Add(order);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The sample orders, oldest first. Past orders are mostly completed; current ones are in progress or waiting;
    /// upcoming ones are new, with one day filled to capacity.
    /// </summary>
    private static IEnumerable<SampleOrder> SampleOrders()
    {
        const OrderStatus inProgress = OrderStatus.InProgress, waiting = OrderStatus.WaitingForCustomer,
            completed = OrderStatus.Completed, cancelled = OrderStatus.Cancelled;
        const string chain = "Chain replacement", brakes = "Brake maintenance", wheel = "Wheel adjustment";

        // Done and gone.
        yield return new("Kari Nordmann", "Trek Domane SL5", chain, -24, -21, [(inProgress, -21, null), (completed, -21, null)], DiscountCode: "WELCOME10");
        yield return new("Lars Johansen", "Specialized Rockhopper", brakes, -22, -20, [(inProgress, -20, null), (completed, -20, null)], Notes: "Rear brake squeals.");
        yield return new("Sofie Larsen", "Giant Escape 2", wheel, -21, -19, [(inProgress, -19, null), (completed, -19, null)]);
        yield return new("Henrik Olsen", "DBS Intruder", chain, -20, -17,
            [(inProgress, -17, null), (waiting, -17, "Cassette is worn too; asked the customer before replacing it"), (inProgress, -16, "Customer said yes"), (completed, -16, null)],
            InternalNotes: "Replaced chain and cassette.", PriceOverride: (1290m, "The cassette was worn as well, so we replaced it together with the chain, as agreed on the phone."));
        yield return new("Emma Andersen", "Merida Big.Nine 300", brakes, -18, -15, [(cancelled, -16, "Customer sold the bike")]);
        yield return new("Jonas Pedersen", "Cannondale Synapse", wheel, -16, -14, [(inProgress, -14, null), (completed, -14, null)], DiscountCode: "SPRING100");
        yield return new("Nora Nilsen", "Scott Aspect 950", chain, -15, -12, [(inProgress, -12, null), (completed, -12, null)], Notes: "Chain skips under load.");
        yield return new("Magnus Kristiansen", "Canyon Grizl 7", brakes, -13, -10, [(inProgress, -10, null), (completed, -9, null)], InternalNotes: "Bled both brakes.");
        yield return new("Ida Jensen", "Bianchi Via Nirone 7", wheel, -11, -8, [(inProgress, -8, null), (completed, -8, null)]);
        yield return new("Erik Karlsen", "Cube Kathmandu", chain, -10, -7, [(cancelled, -9, null)]);
        yield return new("Kari Nordmann", "Trek Domane SL5", brakes, -9, -6, [(inProgress, -6, null), (completed, -5, null)], Notes: "Front brake feels soft.");
        yield return new("Maja Haugen", "Kona Dew", wheel, -7, -4, [(inProgress, -4, null), (completed, -4, null)], DiscountCode: "WELCOME10");
        yield return new("Sander Johannessen", "Riese & Müller Charger4", brakes, -6, -2, [(inProgress, -2, null), (completed, -2, null)]);

        // On the stand now.
        yield return new("Thea Eriksen", "Giant Talon 3", brakes, -5, -1,
            [(inProgress, -1, null), (waiting, -1, "Needs new brake rotors; waiting for the customer to confirm")],
            InternalNotes: "Rotors are 1.4 mm, below the limit.");
        yield return new("Jakob Berg", "Specialized Sirrus", chain, -4, 0, [(inProgress, 0, null)], Notes: "Please call before doing anything extra.");
        yield return new("Sara Dahl", "DBS Cortex", wheel, -3, 0, [(inProgress, 0, null)]);
        yield return new("Mathias Lund", "Trek FX 3", brakes, -3, 1, [(inProgress, 0, null)]);
        yield return new("Hanna Moen", "Merida Speeder 200", chain, -2, 1, []);

        // Coming up.
        yield return new("Tobias Strand", "Cannondale Topstone", wheel, -2, 2, [], Notes: "Rear wheel wobbles.");
        foreach (var (name, bike) in new[] { ("Ella Solberg", "Scott Sub Cross"), ("Filip Bakken", "Giant Revolt"), ("Live Halvorsen", "Cube Nature"), ("Oskar Lie", "Trek Marlin 5") })
        {
            yield return new(name, bike, chain, -1, 2, []); // fills day 2 up to the default capacity of 5
        }

        yield return new("Kari Nordmann", "Trek Domane SL5", wheel, -1, 3, [], Notes: "Booked again for the spring.");
        yield return new("Vilde Aas", "Bianchi Impulso", brakes, 0, 3, []);
        yield return new("Aksel Ruud", "Canyon Endurace", chain, 0, 4, [], DiscountCode: "WELCOME10");
        yield return new("Selma Myhre", "Kona Rove", wheel, 0, 8, []);
        yield return new("Isak Brekke", "Specialized Allez", brakes, 0, 10, []);
    }

    /// <summary>A sample account without a password, so it shows up by name but can't be used to log in.</summary>
    private static async Task<ApplicationUser> AddAccountAsync(UserManager<ApplicationUser> users, string name, string email, string role)
    {
        var user = new ApplicationUser { Name = name, UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user);
        if (result.Succeeded)
        {
            result = await users.AddToRoleAsync(user, role);
        }

        return result.Succeeded
            ? user
            : throw new InvalidOperationException($"Could not create the sample account {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }

    /// <summary>The demo discount codes, added if missing (outside Development they aren't seeded otherwise), by code.</summary>
    private static async Task<Dictionary<string, DiscountCode>> EnsureDiscountCodesAsync(AppDbContext db)
    {
        var existing = await db.DiscountCodes.ToDictionaryAsync(d => d.Code);
        foreach (var code in DiscountSeed.DemoCodes().Where(c => !existing.ContainsKey(c.Code)))
        {
            db.DiscountCodes.Add(code);
            existing[code.Code] = code;
        }

        return existing;
    }

    /// <summary>
    /// A clock for "days from today, at this hour", in the app's time zone, for placing and changing past orders.
    /// A moment later today than now is moved to just before now, keeping the order of the hours.
    /// </summary>
    private static Func<int, int, TimeProvider> Clocks(TimeProvider clock)
    {
        var now = clock.GetLocalNow();
        var startOfToday = new DateTimeOffset(now.Date, now.Offset);
        return (days, hour) =>
        {
            var moment = startOfToday.AddDays(days).AddHours(hour);
            return new FixedClock(moment > now ? now.AddMinutes(hour - 24) : moment, clock.LocalTimeZone);
        };
    }

    /// <summary>"Trek Domane SL5" → ("Trek", "Domane SL5"); two-word brands like "Riese &amp; Müller" are kept whole.</summary>
    private static (string Brand, string? Model) SplitBike(string bike)
    {
        var brand = bike.StartsWith("Riese & Müller") ? "Riese & Müller" : bike.Split(' ')[0];
        var model = bike[brand.Length..].Trim();
        return (brand, model.Length > 0 ? model : null);
    }

    /// <summary>"Lars Johansen" → "lars.johansen@example.com" (example.com is reserved for examples).</summary>
    private static string EmailFor(string name) =>
        $"{name.ToLowerInvariant().Replace(' ', '.').Replace("ø", "o").Replace("å", "a").Replace("æ", "ae")}@example.com";

    /// <summary>A clock stopped at one moment.</summary>
    private sealed class FixedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
