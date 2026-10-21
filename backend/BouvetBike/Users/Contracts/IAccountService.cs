namespace BouvetBike.Users.Contracts;

/// <summary>Accounts and cookie sign-in, on top of ASP.NET Core Identity.</summary>
public interface IAccountService
{
    /// <summary>Creates a Customer account and signs it in.</summary>
    /// <exception cref="Shared.Application.ConflictException">The email is already registered.</exception>
    /// <exception cref="Shared.Application.RequestValidationException">Password doesn't meet the rules, or invalid phone.</exception>
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>Signs in with a cookie.</summary>
    /// <returns>The user, or null for a wrong email or password or a locked-out account (deliberately not told apart).</returns>
    Task<CurrentUserResponse?> LoginAsync(LoginRequest request, CancellationToken ct);

    Task LogoutAsync(CancellationToken ct);

    /// <summary>Null when not logged in.</summary>
    Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken ct);

    Task<IReadOnlyList<StaffUserResponse>> GetStaffAsync(CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">Not a staff member.</exception>
    Task<StaffUserResponse> GetStaffMemberAsync(Guid id, CancellationToken ct);

    /// <exception cref="Shared.Application.ConflictException">The email is already registered.</exception>
    /// <exception cref="Shared.Application.RequestValidationException">Password doesn't meet the rules.</exception>
    Task<StaffUserResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct);
}
