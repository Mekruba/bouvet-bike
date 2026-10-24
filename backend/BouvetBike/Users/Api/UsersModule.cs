using BouvetBike.Shared.Contracts;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users.Application;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Api;

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

    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Users");
        users.MapPost("/", Register);
        users.MapGet("/me", GetMe).WithName(nameof(GetMe)).RequireAuthorization();
        users.MapPost("/{id:guid}/email-confirmation", ConfirmEmail);
        api.MapPost("/email-confirmation-requests", RequestEmailConfirmation).WithTags("Users");

        var session = api.MapGroup("/session").WithTags("Users");
        session.MapPost("/", Login);
        session.MapDelete("/", Logout);

        var staff = api.MapGroup("/admin/staff").WithTags("Users").RequireAuthorization(Policies.Admin);
        staff.MapGet("/", GetStaff);
        staff.MapGet("/{id:guid}", GetStaffMember).WithName(nameof(GetStaffMember));
        staff.MapPost("/", CreateStaff);

        return api;
    }

    private static async Task<CreatedAtRoute<RegistrationResponse>> Register(RegisterRequest request, IAccountService accounts, CancellationToken ct) =>
        TypedResults.CreatedAtRoute(await accounts.RegisterAsync(request, ct), nameof(GetMe));

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> GetMe(IAccountService accounts, CancellationToken ct) =>
        await accounts.GetCurrentAsync(ct) is { } user ? TypedResults.Ok(user) : TypedResults.Unauthorized();

    /// <summary>Wrong email, wrong password and a locked-out account all get the same 401, so it doesn't reveal which accounts exist.</summary>
    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> Login(
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

    /// <summary>What the confirm page calls with the token from the emailed link.</summary>
    private static async Task<NoContent> ConfirmEmail(Guid id, ConfirmEmailRequest request, IAccountService accounts, CancellationToken ct)
    {
        await accounts.ConfirmEmailAsync(id, request.Token, ct);
        return TypedResults.NoContent();
    }

    /// <summary>202: the link would be emailed. Without email delivery it's in the response, for unconfirmed accounts only.</summary>
    private static async Task<Accepted<EmailConfirmationRequestResponse>> RequestEmailConfirmation(
        EmailConfirmationRequest request, IAccountService accounts, CancellationToken ct) =>
        TypedResults.Accepted((string?)null, new EmailConfirmationRequestResponse(await accounts.RequestEmailConfirmationAsync(request.Email, ct)));

    private static async Task<NoContent> Logout(IAccountService accounts, CancellationToken ct)
    {
        await accounts.LogoutAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<StaffUserResponse>>> GetStaff(IAccountService accounts, CancellationToken ct) =>
        TypedResults.Ok(await accounts.GetStaffAsync(ct));

    private static async Task<Ok<StaffUserResponse>> GetStaffMember(Guid id, IAccountService accounts, CancellationToken ct) =>
        TypedResults.Ok(await accounts.GetStaffMemberAsync(id, ct));

    private static async Task<CreatedAtRoute<StaffUserResponse>> CreateStaff(CreateStaffRequest request, IAccountService accounts, CancellationToken ct)
    {
        var created = await accounts.CreateStaffAsync(request, ct);
        return TypedResults.CreatedAtRoute(created, nameof(GetStaffMember), new { id = created.Id });
    }
}
