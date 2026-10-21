using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BouvetBike.Tests;

/// <summary>Helpers for API tests: JSON Patch requests and ProblemDetails assertions.</summary>
public static class ApiAssert
{
    public const string JsonPatchMediaType = "application/json-patch+json";

    /// <summary>Same JSON settings as the API (web defaults, enums as strings).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    /// <summary>GET with extra request headers, e.g. the order contact header.</summary>
    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, string url, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return client.SendAsync(request);
    }

    /// <summary>PATCH with a JSON Patch document and extra request headers.</summary>
    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, string patchDocument, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(patchDocument, Encoding.UTF8, JsonPatchMediaType) };
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return client.SendAsync(request);
    }

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

    /// <summary>
    /// Compares two responses by their JSON. Use for records that contain lists: record equality compares
    /// lists by reference, so two separately loaded but identical responses would never be equal.
    /// </summary>
    public static void AssertSameJson<T>(T expected, T actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected, Json), JsonSerializer.Serialize(actual, Json));

    public static async Task<T> ReadAs<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.AssertStatus(expected);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    // Overloads so a request can be awaited and asserted in one expression.

    public static async Task<JsonElement> AssertStatus(this Task<HttpResponseMessage> response, HttpStatusCode expected) =>
        await (await response).AssertStatus(expected);

    public static async Task<JsonElement> AssertProblem(this Task<HttpResponseMessage> response, HttpStatusCode expected) =>
        await (await response).AssertProblem(expected);

    public static async Task<T> ReadAs<T>(this Task<HttpResponseMessage> response, HttpStatusCode expected = HttpStatusCode.OK) =>
        await (await response).ReadAs<T>(expected);
}
