using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Application;

public sealed class AccountService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ICurrentUser currentUser,
    IEnumerable<ICustomerAccountEvents> accountEvents) : IAccountService
{
    public async Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = NewUser(request.Name, request.Email, NormalizePhone(request.Phone));
        await CreateAsync(user, request.Password, Roles.Customer);
        await signIn.SignInAsync(user, isPersistent: false);
        return new RegistrationResponse(ToCurrentUser(user, [Roles.Customer]), "");
    }

    public Task ConfirmEmailAsync(Guid userId, string token, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public Task<string?> RequestEmailConfirmationAsync(string email, CancellationToken ct) =>
        throw new NotImplementedException("Not yet test-driven.");

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return LoginResult.Failed(LoginFailure.InvalidCredentials);
        }

        // lockoutOnFailure: too many wrong passwords locks the account for a while (Identity defaults: 5 tries).
        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        return result.Succeeded
            ? LoginResult.Success(ToCurrentUser(user, await users.GetRolesAsync(user)))
            : LoginResult.Failed(LoginFailure.InvalidCredentials);
    }

    public Task LogoutAsync(CancellationToken ct) => signIn.SignOutAsync();

    public async Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken ct)
    {
        if (currentUser.Id is not { } id || await users.FindByIdAsync(id.ToString()) is not { } user)
        {
            return null;
        }

        return ToCurrentUser(user, await users.GetRolesAsync(user));
    }

    public async Task<IReadOnlyList<StaffUserResponse>> GetStaffAsync(CancellationToken ct)
    {
        var staff = new Dictionary<Guid, ApplicationUser>();
        foreach (var role in Roles.Staff)
        {
            foreach (var user in await users.GetUsersInRoleAsync(role))
            {
                staff.TryAdd(user.Id, user);
            }
        }

        var result = new List<StaffUserResponse>();
        foreach (var user in staff.Values.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(ToStaff(user, await users.GetRolesAsync(user)));
        }

        return result;
    }

    public async Task<StaffUserResponse> GetStaffMemberAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        var roles = user is null ? [] : await users.GetRolesAsync(user);
        return user is not null && roles.Any(Roles.Staff.Contains)
            ? ToStaff(user, roles)
            : throw new NotFoundException($"Staff member {id} was not found.");
    }

    public async Task<StaffUserResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct)
    {
        var user = NewUser(request.Name, request.Email, phone: null);
        await CreateAsync(user, request.Password, request.Role);
        return ToStaff(user, [request.Role]);
    }

    private static ApplicationUser NewUser(string name, string email, string? phone)
    {
        var normalizedEmail = Email.Create(email).Value;
        return new ApplicationUser { Name = name.Trim(), UserName = normalizedEmail, Email = normalizedEmail, PhoneNumber = phone };
    }

    /// <summary>Creates the user with a role, turning Identity's errors into our API errors.</summary>
    private async Task CreateAsync(ApplicationUser user, string password, string role)
    {
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                throw new ConflictException($"An account with the email '{user.Email}' already exists.");
            }

            var errors = result.Errors
                .GroupBy(e => e.Code.StartsWith("Password") ? "password" : "")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            throw new RequestValidationException("The account could not be created.", errors);
        }

        var roleResult = await users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not add role '{role}': {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
        }
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        try
        {
            return PhoneNumber.Create(phone).Value;
        }
        catch (DomainException ex)
        {
            throw new RequestValidationException(ex.Message, new Dictionary<string, string[]> { ["phone"] = [ex.Message] });
        }
    }

    private static CurrentUserResponse ToCurrentUser(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Name, user.Email!, user.PhoneNumber, roles.Order().ToList());

    private static StaffUserResponse ToStaff(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Name, user.Email!, roles.Order().ToList());
}
