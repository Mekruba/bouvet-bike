using BouvetBike.Shared.Persistence;
using BouvetBike.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace BouvetBike.Tests;

/// <summary>
/// Boots the API against a throwaway SQLite file per factory instance, with <see cref="TestAuthHandler"/>
/// for requests with the test role header (others use the real cookie sign-in), a fixed clock (<see cref="TestData.Today"/>), and the <see cref="TestUsers"/> in the database.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private const string TestOrCookieScheme = "TestOrCookie";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"bouvetbike-test-{Guid.NewGuid()}.db");

    /// <summary>The admin the app seeds from configuration (<c>Admin:Email</c>, <c>Admin:Password</c>).</summary>
    public const string SeededAdminEmail = "admin@bouvetbike.test";
    public const string SeededAdminPassword = "Seeded-Admin-1";

    /// <summary>The app's clock. Starts at <see cref="TestData.Today"/>; tests may advance it.</summary>
    public FakeTimeProvider Clock { get; } = TestData.Clock();

    /// <summary>A client signed in with <paramref name="role"/> (see <see cref="Roles"/>), or anonymous when null.</summary>
    public HttpClient CreateClient(string? role)
    {
        var client = CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        }

        return client;
    }

    /// <summary>Direct database access for test setup that has no API (yet), e.g. discount codes.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
        await db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Admin:Email", SeededAdminEmail);
        builder.UseSetting("Admin:Password", SeededAdminPassword);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);

            // Requests with the test role header use the test scheme; all others use the app's real cookie sign-in,
            // so auth tests exercise the real flow (register, log in, cookie, log out).
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null)
                .AddPolicyScheme(TestOrCookieScheme, null, options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(TestAuthHandler.RoleHeader) ? TestAuthHandler.SchemeName : IdentityConstants.ApplicationScheme);

            // Win over the defaults the app sets (Identity's cookie scheme).
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestOrCookieScheme;
                options.DefaultAuthenticateScheme = TestOrCookieScheme;
                options.DefaultChallengeScheme = TestOrCookieScheme;
                options.DefaultForbidScheme = TestOrCookieScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme;
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Orders and status changes reference users by foreign key, so the test users must exist.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var role in Roles.All)
        {
            var id = TestUsers.IdFor(role);
            if (db.Users.Find(id) is null)
            {
                var email = $"{role.ToLowerInvariant()}@test.bouvetbike.no";
                db.Users.Add(new ApplicationUser { Id = id, UserName = email, Email = email, Name = $"Test {role}" });
            }
        }

        db.SaveChanges();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
