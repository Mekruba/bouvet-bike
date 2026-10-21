namespace BouvetBike.Users;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Mechanic = "Mechanic";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [Admin, Mechanic, Customer];
    public static readonly IReadOnlyList<string> Staff = [Admin, Mechanic];
}
