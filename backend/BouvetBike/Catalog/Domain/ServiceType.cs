using BouvetBike.Shared.Domain;

namespace BouvetBike.Catalog.Domain;

/// <summary>A service the shop offers, e.g. "Chain replacement". Orders copy name and price when placed.</summary>
public sealed class ServiceType : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Money BasePrice { get; private set; } = null!;
    public TimeSpan EstimatedDuration { get; private set; }
    public bool IsActive { get; private set; }

    private ServiceType() { } // EF Core

    /// <summary>A new, active service.</summary>
    /// <exception cref="DomainException">See <see cref="Update"/>.</exception>
    public static ServiceType Create(string name, string? description, Money basePrice, TimeSpan estimatedDuration)
    {
        var service = new ServiceType { IsActive = true };
        service.Update(name, description, basePrice, estimatedDuration);
        return service;
    }

    /// <summary>Changes the details. Orders already placed keep the name and price they copied.</summary>
    /// <exception cref="DomainException">Missing or too long name, too long description, or a duration that isn't positive.</exception>
    public void Update(string name, string? description, Money basePrice, TimeSpan estimatedDuration)
    {
        if (estimatedDuration <= TimeSpan.Zero)
        {
            throw new DomainException("Estimated duration must be positive.");
        }

        Name = Guard.Required(name, "Name", NameMaxLength);
        Description = Guard.Optional(description, "Description", DescriptionMaxLength);
        BasePrice = basePrice;
        EstimatedDuration = estimatedDuration;
    }

    /// <summary>Inactive services can't be ordered, but stay referenced by existing orders.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Makes a retired service orderable again.</summary>
    public void Activate() => IsActive = true;
}
