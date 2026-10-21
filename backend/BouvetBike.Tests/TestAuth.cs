using System.Security.Claims;
using System.Text.Encodings.Web;
using BouvetBike.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BouvetBike.Tests;

/// <summary>Fixed test users, one per role. IDs are stable so tests can assert on them.</summary>
public static class TestUsers
{
    public static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid MechanicId = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    public static readonly Guid CustomerId = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    public static Guid IdFor(string role) => role switch
    {
        Roles.Admin => AdminId,
        Roles.Mechanic => MechanicId,
        Roles.Customer => CustomerId,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

/// <summary>
/// Signs requests in as the role named in the <see cref="RoleHeader"/> header, so API tests can act
/// as admin, mechanic or customer without going through the login flow. No header = anonymous.
/// </summary>
public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role) || string.IsNullOrEmpty(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, TestUsers.IdFor(role!).ToString()),
            new(ClaimTypes.Name, $"Test {role}"),
            new(ClaimTypes.Role, role!),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
