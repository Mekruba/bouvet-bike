using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Users.Contracts;

/// <summary>Customer self-registration. Always creates a user with the Customer role.</summary>
public sealed record RegisterRequest(
    [Required, StringLength(ApplicationUser.NameMaxLength)] string Name,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(32)] string? Phone,
    [Required, StringLength(100, MinimumLength = PasswordRules.MinLength)] string Password);

/// <summary>Email and password for <c>POST /api/session</c>.</summary>
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

/// <summary>Password rules shared by request validation and Identity's options. Identity also requires upper and lower case, a digit and a symbol.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;
}
