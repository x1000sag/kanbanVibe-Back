using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kanban.Tests.Account;

public static class AccountHttp
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<HttpResponseMessage> CreateAccountAsync(HttpClient client, string nick, string password)
    {
        return await client.PostAsJsonAsync("/v1/accounts", new { nick, password }, Json);
    }

    public static async Task<(string Token, DateTimeOffset ExpiresAt)> SignInAsync(HttpClient client, string nick, string password)
    {
        var response = await client.PostAsJsonAsync("/v1/sessions", new { nick, password }, Json);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        var expiresAt = doc.RootElement.GetProperty("expiresAt").GetDateTimeOffset();
        return (token, expiresAt);
    }

    public static void UseToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString()!;
    }
}