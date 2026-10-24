namespace BouvetBike.Users.Contracts;

/// <summary>Accounts, email confirmation and cookie sign-in, on top of ASP.NET Core Identity.</summary>
public interface IAccountService
{
    /// <summary>Creates a Customer account with an unconfirmed email. Doesn't sign in: the email must be confirmed first.</summary>
    /// <exception cref="Shared.Application.ConflictException">The email is already registered.</exception>
    /// <exception cref="Shared.Application.RequestValidationException">Password doesn't meet the rules, or invalid phone.</exception>
    Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>Confirms the email with the token from the link. Idempotent. Links the customer's guest orders.</summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown user, or an invalid or expired token.</exception>
    Task ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);

    /// <summary>A new confirmation link for an unconfirmed account; null otherwise.</summary>
    Task<string?> RequestEmailConfirmationAsync(string email, CancellationToken ct);

    /// <summary>Signs in with a cookie, if the password is right and the email is confirmed.</summary>
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Clears the auth cookie.</summary>
    Task LogoutAsync(CancellationToken ct);

    /// <summary>The logged-in user with their roles. Null when not logged in.</summary>
    Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken ct);

    /// <summary>Every admin and mechanic, sorted by name.</summary>
    Task<IReadOnlyList<StaffUserResponse>> GetStaffAsync(CancellationToken ct);

    /// <summary>One admin or mechanic.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Not a staff member.</exception>
    Task<StaffUserResponse> GetStaffMemberAsync(Guid id, CancellationToken ct);

    /// <summary>Staff accounts are created by an admin, so their email counts as confirmed.</summary>
    /// <exception cref="Shared.Application.ConflictException">The email is already registered.</exception>
    /// <exception cref="Shared.Application.RequestValidationException">Password doesn't meet the rules.</exception>
    Task<StaffUserResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct);
}
