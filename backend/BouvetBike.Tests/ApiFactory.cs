using BouvetBike.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BouvetBike.Tests;

/// <summary>
/// Boots the API against a throwaway SQLite file per factory instance, with <see cref="TestAuthHandler"/>
/// replacing the real sign-in.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"bouvetbike-test-{Guid.NewGuid()}.db");

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);

            // Win over whatever defaults the app sets (e.g. Identity's cookie scheme).
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
