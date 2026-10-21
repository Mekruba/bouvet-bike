using BouvetBike.Catalog.Domain;
using BouvetBike.Orders.Domain;
using BouvetBike.Shared.Domain;
using Microsoft.Extensions.Time.Testing;

namespace BouvetBike.Tests;

internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 3, 2);

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero));

    public static ContactInfo Contact() =>
        new("Kari Nordmann", Email.Create("kari@example.com"), PhoneNumber.Create("912 34 567"));

    public static ServiceType Service(string name = "Chain replacement", decimal price = 790) =>
        ServiceType.Create(name, null, new Money(price), TimeSpan.FromMinutes(45));

    public static MaintenanceOrder Order(TimeProvider clock, ServiceType? service = null) =>
        MaintenanceOrder.Place(Contact(), "Trek", "Domane", service ?? Service(), Today.AddDays(3), clock);
}
