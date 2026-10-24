using System.Buffers.Text;
using System.Text;
using BouvetBike.Shared.Application;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Domain;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Application;

public sealed class AccountService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ICurrentUser currentUser,
    IEnumerable<ICustomerAccountEvents> accountEvents,
    AppDbContext db) : IAccountService
{
    private const string InvalidLink = "This confirmation link is invalid or has expired. Ask for a new one.";

    public async Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = NewUser(request.Name, request.Email, NormalizePhone(request.Phone));
        await CreateAsync(user, request.Password, Roles.Customer);
        return new RegistrationResponse(ToCurrentUser(user, [Roles.Customer]), await ConfirmationLinkAsync(user));
    }

    public async Task ConfirmEmailAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new DomainException(InvalidLink);
        if (user.EmailConfirmed)
        {
            return; // following the link twice is fine
        }

        if (DecodeToken(token) is not { } decoded || !(await users.ConfirmEmailAsync(user, decoded)).Succeeded)
        {
            // Two requests at once (a double click, an email client opening the link first) both see an unconfirmed
            // user; the loser fails Identity's concurrency check. If the other one confirmed it, that's a success.
            await db.Entry(user).ReloadAsync(ct);
            if (user.EmailConfirmed)
            {
                return;
            }

            throw new DomainException(InvalidLink);
        }

        // The address is now proven to be theirs, so other modules may act on it (Orders links guest orders).
        foreach (var handler in accountEvents)
        {
            await handler.CustomerEmailConfirmedAsync(user.Id, user.Email!, ct);
        }
    }

    public async Task<string?> RequestEmailConfirmationAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        return user is null || user.EmailConfirmed ? null : await ConfirmationLinkAsync(user);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return LoginResult.Failed(LoginFailure.InvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            // Identity would refuse before checking the password, which tells anyone the account exists and is
            // unconfirmed. Check the password first (counting failures toward lockout); only then say why.
            if (await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, request.Password))
            {
                await users.AccessFailedAsync(user);
                return LoginResult.Failed(LoginFailure.InvalidCredentials);
            }

            return LoginResult.Failed(LoginFailure.EmailNotConfirmed);
        }

        // lockoutOnFailure: too many wrong passwords locks the account for a while (5 tries, 15 minutes).
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
        user.EmailConfirmed = true; // created by an admin
        await CreateAsync(user, request.Password, request.Role);
        return ToStaff(user, [request.Role]);
    }

    /// <summary>
    /// The frontend page the confirmation email would link to. The token is Base64Url-encoded so it survives
    /// being put in a URL. With real email this would go to the inbox instead of back to the caller.
    /// </summary>
    private async Task<string> ConfirmationLinkAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        return $"/confirm-email?userId={user.Id}&token={Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token))}";
    }

    private static string? DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            return null;
        }
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
