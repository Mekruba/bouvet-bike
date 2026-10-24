using BouvetBike.Shared.Domain;

namespace BouvetBike.Orders.Domain;

/// <summary>Who to contact about an order. A snapshot on the order, so guests can order without an account.</summary>
public sealed record ContactInfo
{
    public const int NameMaxLength = 200;

    public string Name { get; private init; } = null!;
    public Email Email { get; private init; } = null!;
    public PhoneNumber Phone { get; private init; } = null!;

    private ContactInfo() { } // EF Core

    /// <summary>Contact details from already validated email and phone value objects.</summary>
    /// <exception cref="DomainException">Missing or too long name.</exception>
    public ContactInfo(string name, Email email, PhoneNumber phone)
    {
        Name = Guard.Required(name, "Name", NameMaxLength);
        Email = email;
        Phone = phone;
    }
}
