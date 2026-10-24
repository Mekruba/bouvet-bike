namespace BouvetBike.Users;

/// <summary>Authorization policy names, used with <c>RequireAuthorization(...)</c> on endpoints.</summary>
public static class Policies
{
    /// <summary>Admin or Mechanic.</summary>
    public const string Staff = "Staff";

    /// <summary>Admin only.</summary>
    public const string Admin = "Admin";

    /// <summary>A logged-in customer (not staff).</summary>
    public const string Customer = "Customer";
}
