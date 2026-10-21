namespace BouvetBike.Users.Contracts;

/// <summary>Accounts and cookie sign-in, on top of ASP.NET Core Identity.</summary>
public interface IAccountService
{
    /// <summary>Creates a Customer account and signs it in.</summary>
    /// <exception cref="Shared.Domain.DomainException">Email already taken, or the password doesn't meet the rules.</exception>
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <exception cref="Shared.Domain.DomainException">Wrong email or password, or the account is locked out.</exception>
    Task<CurrentUserResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    Task LogoutAsync(CancellationToken ct);

    /// <summary>Null when not logged in.</summary>
    Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken ct);

    Task<IReadOnlyList<StaffUserResponse>> GetStaffAsync(CancellationToken ct);

    /// <exception cref="Shared.Domain.DomainException">Email already taken, or the password doesn't meet the rules.</exception>
    Task<StaffUserResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct);
}
