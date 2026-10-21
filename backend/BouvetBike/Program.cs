using System.Text.Json.Serialization;
using BouvetBike.Orders.Application;
using BouvetBike.Shared.Api;
using BouvetBike.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<CapacityOptions>().BindConfiguration(CapacityOptions.Section).ValidateDataAnnotations().ValidateOnStart();

// Resolved lazily so tests can override ConnectionStrings:Default.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        .UseSeeding(DatabaseSeeder.Seed)
        .UseAsyncSeeding(DatabaseSeeder.SeedAsync));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

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
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();

app.Run();
