using BouvetBike.Shared.Api;
using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users.Application;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Api;

/// <summary>The Users module's wiring: Identity, cookie sign-in, policies, and the account, session and staff endpoints.</summary>
public static class UsersModule
{
    /// <summary>The challenge sent with every 401 (RFC 9110 requires one). Sign-in is a cookie from <c>POST /api/session</c>.</summary>
    private const string Challenge = "Cookie realm=\"BouvetBike\"";

    /// <summary>
    /// ASP.NET Core Identity with cookie sign-in and the role-based policies.
    /// API requests get 401/403 instead of the cookie handler's default redirect to a login page.
    /// </summary>
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = PasswordRules.MinLength;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders(); // email confirmation tokens

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "bouvetbike.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = Challenge;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, policy => policy.RequireRole(Roles.Admin, Roles.Mechanic))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(Policies.Customer, policy => policy.RequireRole(Roles.Customer));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IAccountService, AccountService>();
        return services;
    }

    /// <summary>
    /// Maps registration and email confirmation (<c>/api/users</c>, <c>/api/email-confirmation-requests</c>),
    /// login and logout (<c>/api/session</c>), and staff accounts (<c>/api/admin/staff</c>, Admin only).
    /// All of them return personal data or tokens, so none may be cached.
    /// </summary>
    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Users").WithNoStore();
        users.MapPost("/", Register);
        users.MapGet("/me", GetMe).WithName(nameof(GetMe)).RequireAuthorization();
        users.MapPut("/{id:guid}/email-confirmation", ConfirmEmail);
        api.MapPost("/email-confirmation-requests", RequestEmailConfirmation).WithTags("Users").WithNoStore();

        var session = api.MapGroup("/session").WithTags("Users").WithNoStore();
        session.MapPost("/", Login);
        session.MapDelete("/", Logout);

        var staff = api.MapGroup("/admin/staff").WithTags("Users").RequireAuthorization(Policies.Admin).WithNoStore();
        staff.MapGet("/", GetStaff);
        staff.MapGet("/{id:guid}", GetStaffMember).WithName(nameof(GetStaffMember));
        staff.MapPost("/", CreateStaff);

        return api;
    }

    /// <summary>Register as a customer</summary>
    /// <remarks>
    /// 201 with the new customer and the email confirmation link, <c>Location: /api/users/me</c>. Doesn't sign in:
    /// the email must be confirmed first. 409 if the email is taken; 400 for a weak password or invalid phone.
    /// </remarks>
    internal static async Task<CreatedAtRoute<RegistrationResponse>> Register(RegisterRequest request, IAccountService accounts, CancellationToken ct) =>
        TypedResults.CreatedAtRoute(await accounts.RegisterAsync(request, ct), nameof(GetMe));

    /// <summary>Get the logged-in user</summary>
    /// <remarks>With their roles. 401 when not logged in, or the account no longer exists.</remarks>
    internal static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> GetMe(IAccountService accounts, CancellationToken ct) =>
        await accounts.GetCurrentAsync(ct) is { } user ? TypedResults.Ok(user) : TypedResults.Unauthorized();

    /// <summary>Log in</summary>
    /// <remarks>
    /// 200 with the user and the auth cookie; 403 when the password is right but the email isn't confirmed.
    /// Wrong email, wrong password and a locked-out account all get the same 401, so it doesn't reveal which accounts exist.
    /// </remarks>
    internal static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> Login(
        LoginRequest request, IAccountService accounts, HttpResponse response, CancellationToken ct)
    {
        var result = await accounts.LoginAsync(request, ct);
        if (result.User is { } user)
        {
            return TypedResults.Ok(user);
        }

        if (result.Failure == LoginFailure.EmailNotConfirmed)
        {
            // The password was right, so saying why reveals nothing to someone who doesn't own the account.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Email not confirmed",
                detail: "Confirm your email before logging in. Use the link we sent you, or ask for a new one.");
        }

        response.Headers.WWWAuthenticate = Challenge;
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: "Invalid email or password.");
    }

    /// <summary>Confirm an email</summary>
    /// <remarks>
    /// What the confirm page calls with the token from the emailed link. Links the customer's guest orders with that email.
    /// 204, also when already confirmed (idempotent); 400 for an unknown user or a bad token.
    /// </remarks>
    internal static async Task<NoContent> ConfirmEmail(Guid id, ConfirmEmailRequest request, IAccountService accounts, CancellationToken ct)
    {
        await accounts.ConfirmEmailAsync(id, request.Token, ct);
        return TypedResults.NoContent();
    }

    /// <summary>Ask for a new confirmation link</summary>
    /// <remarks>
    /// 202, since the link would be emailed. Without email delivery it's in the response, for unconfirmed accounts only;
    /// otherwise the link is null.
    /// </remarks>
    internal static async Task<Accepted<EmailConfirmationRequestResponse>> RequestEmailConfirmation(
        EmailConfirmationRequest request, IAccountService accounts, CancellationToken ct) =>
        TypedResults.Accepted((string?)null, new EmailConfirmationRequestResponse(await accounts.RequestEmailConfirmationAsync(request.Email, ct)));

    /// <summary>Log out</summary>
    /// <remarks>204 and the cookie is cleared, also when not logged in.</remarks>
    internal static async Task<NoContent> Logout(IAccountService accounts, CancellationToken ct)
    {
        await accounts.LogoutAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>List the staff</summary>
    /// <remarks>Admin only. Every admin and mechanic, sorted by name.</remarks>
    internal static async Task<Ok<IReadOnlyList<StaffUserResponse>>> GetStaff(IAccountService accounts, CancellationToken ct) =>
        TypedResults.Ok(await accounts.GetStaffAsync(ct));

    /// <summary>Get a staff member</summary>
    /// <remarks>Admin only. 404 if unknown or a customer.</remarks>
    internal static async Task<Ok<StaffUserResponse>> GetStaffMember(Guid id, IAccountService accounts, CancellationToken ct) =>
        TypedResults.Ok(await accounts.GetStaffMemberAsync(id, ct));

    /// <summary>Add a staff member</summary>
    /// <remarks>Admin only. An admin or mechanic, with a confirmed email. 201 with the new account and its <c>Location</c>; 409 if the email is taken.</remarks>
    internal static async Task<CreatedAtRoute<StaffUserResponse>> CreateStaff(CreateStaffRequest request, IAccountService accounts, CancellationToken ct)
    {
        var created = await accounts.CreateStaffAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetStaffMember), new { id = created.Id });
    }
}
