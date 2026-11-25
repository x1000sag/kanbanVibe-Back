using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class SignInTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task WrongPasswordAndMissingNick_SameSignInFailed()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");

        var wrong = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "marina", password = "nope" }, AccountHttp.Json);
        var missing = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "no-such-nick", password = "x" }, AccountHttp.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("sign_in_failed", await AccountHttp.ReadErrorCodeAsync(wrong));
        Assert.Equal("sign_in_failed", await AccountHttp.ReadErrorCodeAsync(missing));
    }

    [Fact]
    public async Task Success_ReturnsJwtWithClaims()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var response = await _client.PostAsJsonAsync("/v1/sessions", new { nick = "marina", password = "secret" }, AccountHttp.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (token, expiresAt) = await AccountHttp.SignInAsync(_client, "marina", "secret");

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
    public async Task TwoSignIns_TwoDifferentTokens()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var (t1, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        var (t2, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        Assert.NotEqual(t1, t2);
    }
}