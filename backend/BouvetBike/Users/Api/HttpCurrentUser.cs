using System.Security.Claims;
using BouvetBike.Shared.Contracts;

namespace BouvetBike.Users.Api;

/// <summary><see cref="ICurrentUser"/> from the signed-in principal of the current HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid? Id =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <inheritdoc />
    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}
