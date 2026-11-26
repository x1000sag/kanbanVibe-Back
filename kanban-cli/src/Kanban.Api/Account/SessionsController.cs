using System.Text.Json.Serialization;
using Account.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Account;

[ApiController]
[Route("v1/sessions")]
[RequestSizeLimit(AccountRequestReader.MaxBodyBytes)]
public sealed class SessionsController : ControllerBase
{
    private readonly SignInService _signIn;
    private readonly SessionEndService _sessionEnd;
    private readonly AccessGuard _access;

    public SessionsController(SignInService signIn, SessionEndService sessionEnd, AccessGuard access)
    {
        _signIn = signIn;
        _sessionEnd = sessionEnd;
        _access = access;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn(CancellationToken ct)
    {
        using var doc = await AccountRequestReader.ReadStrictObjectAsync(Request, "nick", "password");
        var nick = AccountRequestReader.RequireString(doc.RootElement, "nick");
        var password = AccountRequestReader.RequireString(doc.RootElement, "password");
        var source = RequestSource.FromRemoteAddress(HttpContext.Connection.RemoteIpAddress);

        var (token, expiresAt) = await _signIn.SignInAsync(nick, password, source, ct);
        return Ok(new SessionResponse(token, expiresAt));
    }

    [HttpDelete("current")]
    [Authorize]
    public async Task<IActionResult> EndCurrent(CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        await _sessionEnd.EndAccessAsync(active.AccountId, active.AccessId, ct);
        return NoContent();
    }
}

public sealed record SessionResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);