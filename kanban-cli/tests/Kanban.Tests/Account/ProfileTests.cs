using System.Net;
using System.Text.Json;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class ProfileTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Owner_ReadsProfileWithDefaultColumns()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var (token, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        AccountHttp.UseToken(_client, token);

        var response = await _client.GetAsync("/v1/accounts/marina");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("marina", doc.RootElement.GetProperty("nick").GetString());
        var columns = doc.RootElement.GetProperty("board").GetProperty("columns");
        Assert.Equal(3, columns.GetArrayLength());
    }

    [Fact]
    public async Task ForeignNick_Returns403()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        await AccountHttp.CreateAccountAsync(_client, "Marina", "secret");
        var (token, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        AccountHttp.UseToken(_client, token);

        var response = await _client.GetAsync("/v1/accounts/Marina");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("foreign_data", await AccountHttp.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var response = await _client.GetAsync("/v1/accounts/marina");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no_active_access", await AccountHttp.ReadErrorCodeAsync(response));
    }
}