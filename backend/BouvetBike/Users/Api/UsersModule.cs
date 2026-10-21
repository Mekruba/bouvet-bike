using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace BouvetBike.Users.Api;

public static class UsersModule
{
    /// <summary>
    /// Cookie authentication (Identity's application scheme) and the role-based policies.
    /// API requests get 401/403 instead of the cookie handler's default redirect to a login page.
    /// </summary>
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "bouvetbike.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Staff, policy => policy.RequireRole(Roles.Admin, Roles.Mechanic))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

        return services;
    }

    private static Task SetStatus(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}
