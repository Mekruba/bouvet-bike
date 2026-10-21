using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users;

/// <summary>
/// A login: customer or staff, told apart by <see cref="Roles"/>. Email and PhoneNumber come from
/// <see cref="IdentityUser{TKey}"/>. The domain refers to users by ID only.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public const int NameMaxLength = 200;

    public string Name { get; set; } = "";
}
