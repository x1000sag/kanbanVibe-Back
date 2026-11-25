using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class RegistrationTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateMarina_Returns201AndLocation()
    {
        var response = await AccountHttp.CreateAccountAsync(_client, "  marina  ", "любой-пароль");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/v1/accounts/marina", response.Headers.Location?.ToString());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("marina", doc.RootElement.GetProperty("nick").GetString());
        Assert.Equal("marina", doc.RootElement.GetProperty("displayName").GetString());
        Assert.False(doc.RootElement.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task DuplicateNick_Returns409()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "p1");
        var dup = await AccountHttp.CreateAccountAsync(_client, "marina", "p2");
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("nick_taken", await AccountHttp.ReadErrorCodeAsync(dup));
    }

    [Fact]
    public async Task MarinaAndMarinaCaseDiffer_TwoAccounts()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "p1");
        var second = await AccountHttp.CreateAccountAsync(_client, "Marina", "p2");
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task InvalidNickAndPassword_DifferentCodes()
    {
        var emptyNick = await AccountHttp.CreateAccountAsync(_client, "", "ok");
        Assert.Equal("nick_invalid", await AccountHttp.ReadErrorCodeAsync(emptyNick));

        var badPwd = await AccountHttp.CreateAccountAsync(_client, "nick1", "");
        Assert.Equal("password_invalid", await AccountHttp.ReadErrorCodeAsync(badPwd));
    }

    [Fact]
    public async Task ExtraField_ReturnsRequestInvalid()
    {
        var json = """{"nick":"x","password":"y","extra":1}""";
        var response = await _client.PostAsync("/v1/accounts", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request_invalid", await AccountHttp.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task RaceSameNick_OneSucceedsOneNickTaken()
    {
        var t1 = AccountHttp.CreateAccountAsync(_client, "race", "p");
        var t2 = AccountHttp.CreateAccountAsync(_client, "race", "p");
        await Task.WhenAll(t1, t2);
        var r1 = await t1;
        var r2 = await t2;
        var codes = new[]
        {
            r1.IsSuccessStatusCode ? "ok" : await AccountHttp.ReadErrorCodeAsync(r1),
            r2.IsSuccessStatusCode ? "ok" : await AccountHttp.ReadErrorCodeAsync(r2),
        };
        Assert.Contains("ok", codes);
        Assert.Contains("nick_taken", codes);
    }
}