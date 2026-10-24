using System.Net;
using BouvetBike.Users.Contracts;
using Microsoft.AspNetCore.WebUtilities;

namespace BouvetBike.Tests.Users;

/// <summary>Register, confirm the email via the returned link, log in: the real customer sign-up flow.</summary>
internal static class AccountTestHelpers
{
    public const string Password = "Sykkel-2026";

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> Register(this HttpClient client, string email, string? phone = null, string name = "Kari Nordmann") =>
        client.PostJsonAsync("/api/users", new RegisterRequest(name, email, phone, Password));

    public static Task<RegistrationResponse> RegisterOk(this HttpClient client, string email, string? phone = null) =>
        client.Register(email, phone).ReadAs<RegistrationResponse>(HttpStatusCode.Created);

    /// <summary>What clicking the link in the confirmation email does: the confirm page sends the token with a PUT.</summary>
    public static Task<HttpResponseMessage> FollowConfirmationLink(this HttpClient client, string link)
    {
        var query = QueryHelpers.ParseQuery(new Uri(new Uri("http://localhost"), link).Query);
        return client.PutJsonAsync($"/api/users/{query["userId"]}/email-confirmation", new ConfirmEmailRequest(query["token"]!));
    }

    public static Task<HttpResponseMessage> Login(this HttpClient client, string email, string password = Password) =>
        client.PostJsonAsync("/api/session", new LoginRequest(email, password));

    /// <summary>A client signed in as a newly registered, confirmed customer.</summary>
    public static async Task<HttpClient> SignedInCustomer(this ApiFactory factory, string email)
    {
        var client = factory.CreateClient(role: null);
        var registration = await client.RegisterOk(email);
        await client.FollowConfirmationLink(registration.EmailConfirmationLink).AssertStatus(HttpStatusCode.NoContent);
        await client.Login(email).AssertStatus(HttpStatusCode.OK);
        return client;
    }
}
