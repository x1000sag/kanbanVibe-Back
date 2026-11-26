using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Infrastructure;
using Npgsql;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class SignInTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task WrongPasswordAndMissingNick_SameSignInFailed()
    {
        await AccountHttp.CreateAccountAsync(_client, "signin-a", "secret");

        var wrong = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "signin-a", password = "nope" }, AccountHttp.Json);
        var missing = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "no-such-nick", password = "x" }, AccountHttp.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("sign_in_failed", await AccountHttp.ReadErrorCodeAsync(wrong));
        Assert.Equal("sign_in_failed", await AccountHttp.ReadErrorCodeAsync(missing));
    }

    [Fact]
    public async Task Success_ReturnsJwtWithClaims()
    {
        var create = await AccountHttp.CreateAccountAsync(_client, "jwt-user", "secret");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var response = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "jwt-user", password = "secret" }, AccountHttp.Json);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var session = JsonDocument.Parse(body);
        var token = session.RootElement.GetProperty("accessToken").GetString()!;
        var expiresAt = session.RootElement.GetProperty("expiresAt").GetDateTimeOffset();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal("kanban-account", jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == "kanban-account");
        Assert.NotNull(jwt.Subject);
        Assert.NotNull(jwt.Id);
        Assert.True((jwt.ValidTo - jwt.ValidFrom).TotalSeconds <= 43200);
        Assert.True(expiresAt <= jwt.ValidTo);
    }

    [Fact]
    public async Task CyrillicNick_SignInFailedAndDoesNotOpenAccess()
    {
        const string nick = "нет-такого";
        var response = await _client.PostAsJsonAsync(
            "/v1/sessions",
            new { nick, password = "после" },
            AccountHttp.Json);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("sign_in_failed", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoredCyrillicNick_IsNotIdentified()
    {
        const string nick = "уже-есть";
        const string password = "после";
        var hash = new AccountPasswordHasher().Hash(password);

        await using (var conn = new NpgsqlConnection(factory.AppConnection))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO account (account_id, nick, display_name, password_hash)
                VALUES (@id, @nick, @nick, @hash)
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("nick", nick);
            cmd.Parameters.AddWithValue("hash", hash);
            await cmd.ExecuteNonQueryAsync();
        }

        var response = await _client.PostAsJsonAsync(
            "/v1/sessions",
            new { nick, password },
            AccountHttp.Json);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("sign_in_failed", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoSignIns_TwoDifferentTokens()
    {
        await AccountHttp.CreateAccountAsync(_client, "two-tok", "secret");
        var (t1, _) = await AccountHttp.SignInAsync(_client, "two-tok", "secret");
        var (t2, _) = await AccountHttp.SignInAsync(_client, "two-tok", "secret");
        Assert.NotEqual(t1, t2);
    }
}