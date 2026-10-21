namespace BouvetBike.Orders.Contracts;

/// <summary>Max lengths for raw contact input. The domain normalizes and validates the format.</summary>
internal static class ContactInputLimits
{
    public const int Phone = 32;
    public const int EmailOrPhone = 254;
}
