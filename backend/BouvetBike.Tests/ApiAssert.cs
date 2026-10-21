using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BouvetBike.Tests;

/// <summary>Helpers for API tests: JSON Patch requests and ProblemDetails assertions.</summary>
public static class ApiAssert
{
    public const string JsonPatchMediaType = "application/json-patch+json";

    /// <summary>Sends <paramref name="patchDocument"/> (a JSON array of RFC 6902 operations) as a PATCH.</summary>
    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, string patchDocument, string mediaType = JsonPatchMediaType) =>
        client.PatchAsync(url, new StringContent(patchDocument, Encoding.UTF8, mediaType));

    /// <summary>Asserts the status code and returns the body as JSON (ProblemDetails for errors).</summary>
    public static async Task<JsonElement> AssertStatus(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Asserts an RFC 7807 problem response with the given status.</summary>
    public static async Task<JsonElement> AssertProblem(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var problem = await response.AssertStatus(expected);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        return problem;
    }

    /// <summary>Asserts the problem has a field error for <paramref name="field"/> (matched case-insensitively).</summary>
    public static void AssertFieldError(this JsonElement problem, string field) =>
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => error.Name.Equals(field, StringComparison.OrdinalIgnoreCase));

    public static async Task<T> ReadAs<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.AssertStatus(expected);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    // Overloads so a request can be awaited and asserted in one expression.

    public static async Task<JsonElement> AssertStatus(this Task<HttpResponseMessage> response, HttpStatusCode expected) =>
        await (await response).AssertStatus(expected);

    public static async Task<JsonElement> AssertProblem(this Task<HttpResponseMessage> response, HttpStatusCode expected) =>
        await (await response).AssertProblem(expected);

    public static async Task<T> ReadAs<T>(this Task<HttpResponseMessage> response, HttpStatusCode expected = HttpStatusCode.OK) =>
        await (await response).ReadAs<T>(expected);
}
