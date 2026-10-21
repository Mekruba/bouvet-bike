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
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

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

        var session = api.MapGroup("/session").WithTags("Users");
        session.MapPost("/", Login);
        session.MapDelete("/", Logout);

        var staff = api.MapGroup("/admin/staff").WithTags("Users").RequireAuthorization(Policies.Admin);
        staff.MapGet("/", GetStaff);
        staff.MapGet("/{id:guid}", GetStaffMember).WithName(nameof(GetStaffMember));
        staff.MapPost("/", CreateStaff);

        return api;
    }

    private static async Task<CreatedAtRoute<CurrentUserResponse>> Register(RegisterRequest request, IAccountService accounts, CancellationToken ct) =>
        TypedResults.CreatedAtRoute(await accounts.RegisterAsync(request, ct), nameof(GetMe));

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> GetMe(IAccountService accounts, CancellationToken ct) =>
        await accounts.GetCurrentAsync(ct) is { } user ? TypedResults.Ok(user) : TypedResults.Unauthorized();

    /// <summary>Wrong email, wrong password and a locked-out account all get the same 401, so it doesn't reveal which accounts exist.</summary>
    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> Login(
        LoginRequest request, IAccountService accounts, HttpResponse response, CancellationToken ct)
    {
        if (await accounts.LoginAsync(request, ct) is { } user)
        {
            return TypedResults.Ok(user);
        }

        response.Headers.WWWAuthenticate = Challenge;
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: "Invalid email or password.");
    }

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
