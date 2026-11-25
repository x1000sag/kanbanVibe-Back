using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class BoardTests(AccountApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateCard_OnOwnerBoard()
    {
        await AccountHttp.CreateAccountAsync(_client, "marina", "secret");
        var (token, _) = await AccountHttp.SignInAsync(_client, "marina", "secret");
        AccountHttp.UseToken(_client, token);

        var body = new
        {
            columnName = "ToDo",
            title = "Купить молоко",
            description = "2л",
            tags = new[] { "дом" },
            deadline = "2026-11-20",
        };
        var response = await _client.PostAsJsonAsync("/v1/accounts/marina/cards", body, AccountHttp.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("2026-11-20", doc.RootElement.GetProperty("deadline").GetString());
    }
}