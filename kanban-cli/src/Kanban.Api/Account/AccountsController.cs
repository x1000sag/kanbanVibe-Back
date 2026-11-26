using System.Text.Json.Serialization;
using Account.Domain;
using Account.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Account;

[ApiController]
[Route("v1/accounts")]
[RequestSizeLimit(AccountRequestReader.MaxBodyBytes)]
public sealed class AccountsController : ControllerBase
{
    private readonly RegistrationService _registration;
    private readonly ProfileReader _profiles;
    private readonly AccountUpdateService _updates;
    private readonly AccessGuard _access;

    public AccountsController(
        RegistrationService registration,
        ProfileReader profiles,
        AccountUpdateService updates,
        AccessGuard access)
    {
        _registration = registration;
        _profiles = profiles;
        _updates = updates;
        _access = access;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        using var doc = await AccountRequestReader.ReadStrictObjectAsync(Request, "nick", "password");
        var nick = AccountRequestReader.RequireString(doc.RootElement, "nick");
        var password = AccountRequestReader.RequireString(doc.RootElement, "password");

        var (validNick, displayName) = await _registration.RegisterAsync(nick, password, ct);
        Response.Headers.Location = $"/v1/accounts/{validNick}";
        return StatusCode(201, new IdentityResponse(validNick, displayName));
    }

    [HttpGet("{nick}")]
    [Authorize]
    public async Task<IActionResult> GetProfile(string nick, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        var profile = await _profiles.ReadAsync(active.AccountId, ct);
        return Ok(ProfileResponse.From(profile));
    }

    [HttpPut("{nick}/display-name")]
    [Authorize]
    public async Task<IActionResult> ChangeDisplayName(string nick, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        using var doc = await AccountRequestReader.ReadStrictObjectAsync(Request, "displayName");
        var displayName = AccountRequestReader.RequireString(doc.RootElement, "displayName");

        try
        {
            var (validNick, validName) = await _updates.ChangeDisplayNameAsync(active.AccountId, displayName, ct);
            return Ok(new IdentityResponse(validNick, validName));
        }
        catch (AccountRefusalException ex) when (ex.Code == RefusalCodes.DisplayNameInvalid)
        {
            throw;
        }
    }

    [HttpPut("{nick}/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(string nick, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        using var doc = await AccountRequestReader.ReadStrictObjectAsync(Request, "currentPassword", "newPassword");
        var current = AccountRequestReader.RequireString(doc.RootElement, "currentPassword");
        var newPassword = AccountRequestReader.RequireString(doc.RootElement, "newPassword");

        await _updates.ChangePasswordAsync(active.AccountId, current, newPassword, ct);
        return NoContent();
    }
}

public sealed record IdentityResponse(
    [property: JsonPropertyName("nick")] string Nick,
    [property: JsonPropertyName("displayName")] string DisplayName);

public sealed record ProfileResponse(
    [property: JsonPropertyName("nick")] string Nick,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("board")] BoardResponse Board)
{
    public static ProfileResponse From(ProfileDto dto) => new(
        dto.Nick,
        dto.DisplayName,
        new BoardResponse(dto.Columns.Select(c => new ColumnResponse(
            c.Name,
            c.Cards.Select(card => new CardResponse(
                card.Id,
                card.Title,
                card.Description,
                card.Tags,
                card.Deadline,
                card.CreatedAt)).ToList())).ToList()));
}

public sealed record BoardResponse([property: JsonPropertyName("columns")] IReadOnlyList<ColumnResponse> Columns);

public sealed record ColumnResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cards")] IReadOnlyList<CardResponse> Cards);

public sealed record CardResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("deadline")] string? Deadline,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);