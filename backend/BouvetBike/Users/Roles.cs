namespace BouvetBike.Users;

/// <summary>Identity role names. Every account has one.</summary>
public static class Roles
{
    /// <summary>Everything a mechanic can do, plus the service catalog, price overrides, discount codes, capacity, staff accounts and deleting orders.</summary>
    public const string Admin = "Admin";

    /// <summary>Works on orders: status, notes and details.</summary>
    public const string Mechanic = "Mechanic";

    /// <summary>A registered customer; sees the orders linked to the account.</summary>
    public const string Customer = "Customer";

    /// <summary>Every role, created on startup.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Mechanic, Customer];

    /// <summary>The roles that may use the staff area.</summary>
    public static readonly IReadOnlyList<string> Staff = [Admin, Mechanic];
}
