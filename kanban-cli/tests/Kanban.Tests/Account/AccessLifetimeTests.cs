using System.Net;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class AccessLifetimeTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EndOneToken_LeavesOtherActive()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var (t1, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        var (t2, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");

        AccountHttp.UseToken(_client, t1);
        var end = await _client.DeleteAsync("/v1/sessions/current");
        Assert.Equal(HttpStatusCode.NoContent, end.StatusCode);

        AccountHttp.UseToken(_client, t1);
        var blocked = await _client.GetAsync("/v1/accounts/marina");
        Assert.Equal("no_active_access", await AccountHttp.ReadErrorCodeAsync(blocked));

        AccountHttp.UseToken(_client, t2);
        var ok = await _client.GetAsync("/v1/accounts/marina");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task After12Hours_TokenExpires()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var (token, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        factory.Time.Advance(TimeSpan.FromHours(12));

        AccountHttp.UseToken(_client, token);
        var response = await _client.GetAsync("/v1/accounts/marina");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no_active_access", await AccountHttp.ReadErrorCodeAsync(response));
    }
}