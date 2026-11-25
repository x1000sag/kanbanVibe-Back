using System.Text.Json;
using Account.Domain;
using Account.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Account;

[ApiController]
[Route("v1/accounts/{nick}")]
[RequestSizeLimit(AccountRequestReader.MaxBodyBytes)]
public sealed class BoardController : ControllerBase
{
    private readonly AccessGuard _access;
    private readonly BoardWriter _writer;

    public BoardController(AccessGuard access, BoardWriter writer)
    {
        _access = access;
        _writer = writer;
    }

    [HttpPost("cards")]
    [Authorize]
    public async Task<IActionResult> CreateCard(string nick, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        using var doc = await AccountRequestReader.ReadStrictObjectAsync(
            Request,
            "columnName",
            "title",
            "description",
            "tags",
            "deadline");

        var columnName = AccountRequestReader.RequireString(doc.RootElement, "columnName");
        var title = AccountRequestReader.RequireString(doc.RootElement, "title");
        var description = AccountRequestReader.OptionalNullableString(doc.RootElement, "description");
        var tags = AccountRequestReader.OptionalStringArray(doc.RootElement, "tags");
        string? deadline = null;
        if (doc.RootElement.TryGetProperty("deadline", out var deadlineEl))
        {
            if (deadlineEl.ValueKind == JsonValueKind.Null)
            {
                deadline = null;
            }
            else if (deadlineEl.ValueKind == JsonValueKind.String)
            {
                deadline = deadlineEl.GetString();
            }
            else
            {
                throw new AccountRefusalException(RefusalCodes.RequestInvalid);
            }
        }

        var card = await _writer.CreateCardAsync(
            active.AccountId,
            columnName,
            title,
            description,
            tags,
            deadline,
            ct);

        return StatusCode(201, new CardResponse(
            card.Id,
            card.Title,
            card.Description,
            card.Tags,
            card.Deadline,
            card.CreatedAt));
    }

    [HttpDelete("cards/{cardId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteCard(string nick, Guid cardId, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        await _writer.DeleteCardAsync(active.AccountId, cardId, ct);
        return NoContent();
    }

    [HttpDelete("columns/{columnName}")]
    [Authorize]
    public async Task<IActionResult> DeleteColumn(string nick, string columnName, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        await _writer.DeleteColumnAsync(active.AccountId, columnName, ct);
        return NoContent();
    }
}