using System.Text.Json.Serialization;
using BouvetBike.Catalog.Api;
using BouvetBike.Discounts.Api;
using BouvetBike.Orders.Api;
using BouvetBike.Orders.Persistence;
using BouvetBike.Shared.Api;
using BouvetBike.Shared.Persistence;
using BouvetBike.Users.Api;
using BouvetBike.Users.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The keys sign the auth cookie and email confirmation tokens; in a container they must outlive the container.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddUsersModule();
builder.Services.AddCatalogModule();
builder.Services.AddDiscountsModule();
builder.Services.AddOrdersModule();

// Resolved lazily so tests can override ConnectionStrings:Default.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        .UseSeeding(DatabaseSeeder.Seed)
        .UseAsyncSeeding(DatabaseSeeder.SeedAsync));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// No migrations: schema is created from the model on startup.
// After changing the model, delete bouvetbike.db (`just reset-db`) or switch to `dotnet ef migrations`.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    await CapacitySeeder.SeedAsync(scope.ServiceProvider);

    if (app.Environment.IsDevelopment())
    {
        await DemoDataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapCatalogEndpoints();
api.MapOrdersEndpoints();
api.MapUsersEndpoints();
api.MapDiscountsEndpoints();

app.Run();
