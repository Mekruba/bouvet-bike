namespace BouvetBike.Users;

/// <summary>Authorization policy names, used with <c>RequireAuthorization(...)</c> on endpoints.</summary>
public static class Policies
{
    /// <summary>Admin or Mechanic.</summary>
    public const string Staff = "Staff";

    public const string Admin = "Admin";
}
