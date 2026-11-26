using System.Net;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class LiteralTextTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

  public static TheoryData<string> LiteralValues { get; } = new()
    {
        """nick'quote""",
        """'; DROP TABLE account;--""",
        "' OR '1'='1",
        "SELECT * FROM account",
        "Robert'); DROP TABLE Students;--",
        "<script>alert(1)</script>",
        "\"onload=\"x",
        "\\x00nick",
        "nick\twith",
        "  edge  ",
    };

    [Theory]
    [MemberData(nameof(LiteralValues))]
    public async Task Nick_StoredAndReadLiterally(string literal)
    {
        var nick = "lit-" + Guid.NewGuid().ToString("N")[..6];
        await AccountHttp.CreateAccountAsync(_client, nick, literal.Length > 0 ? literal : "pw");
        var (token, _) = await AccountHttp.SignInAsync(_client, nick, literal.Length > 0 ? literal : "pw");
        AccountHttp.UseToken(_client, token);
        await AccountHttp.CreateAccountAsync(_client, "other-" + Guid.NewGuid().ToString("N")[..6], "pw2");

        var profile = await _client.GetAsync($"/v1/accounts/{nick}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = await profile.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }
}