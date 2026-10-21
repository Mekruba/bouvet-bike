using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Users.Contracts;

/// <summary>Customer self-registration. Always creates a user with the Customer role.</summary>
public sealed record RegisterRequest(
    [Required, StringLength(ApplicationUser.NameMaxLength)] string Name,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(32)] string? Phone,
    [Required, StringLength(100, MinimumLength = PasswordRules.MinLength)] string Password);

/// <param name="RememberMe">Persistent cookie instead of a session cookie.</param>
public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

/// <summary>Admin creating a staff account.</summary>
public sealed record CreateStaffRequest(
    [Required, StringLength(ApplicationUser.NameMaxLength)] string Name,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = PasswordRules.MinLength)] string Password,
    [Required, AllowedValues(Roles.Admin, Roles.Mechanic)] string Role);

public static class PasswordRules
{
    public const int MinLength = 8;
}
