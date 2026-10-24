using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Persistence;

/// <summary>
/// Creates the roles, and the first admin from configuration (<c>Admin:Email</c>, <c>Admin:Password</c>).
/// Credentials come from user secrets or environment variables, never from committed files.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Runs on every start: creates missing roles, then the admin if configured and not there yet.
    /// Without configuration it logs a warning and creates no admin.
    /// </summary>
    /// <exception cref="InvalidOperationException">Identity rejected the configured admin, e.g. a weak password.</exception>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var config = services.GetRequiredService<IConfiguration>();
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder))
                .LogWarning("No admin configured (Admin:Email, Admin:Password); no admin account was seeded.");
            return;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser { Name = "Administrator", UserName = email.ToLowerInvariant(), Email = email.ToLowerInvariant(), EmailConfirmed = true };
        var result = await users.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            result = await users.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not seed the admin account: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
