using System.Text.Json.Serialization;
using Account.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Account;

[ApiController]
[Route("v1/accounts/{nick}/journal")]
public sealed class JournalController : ControllerBase
{
    private readonly AccessGuard _access;
    private readonly JournalReader _journal;

    public JournalController(AccessGuard access, JournalReader journal)
    {
        _access = access;
        _journal = journal;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetJournal(string nick, CancellationToken ct)
    {
        var active = await _access.RequireActiveAccessAsync(User, ct);
        if (active.Nick != nick)
        {
            await _access.DenyForeignAndLogAsync(active, ct);
        }

        var journal = await _journal.ReadAsync(active.AccountId, active.Nick, ct);
        return Ok(new JournalResponse(journal.Events.Select(e => new JournalEventResponse(
            e.OccurredAt,
            e.Nick,
            e.Type,
            e.Outcome,
            e.Source,
            e.ObjectName)).ToList()));
    }
}

public sealed record JournalResponse([property: JsonPropertyName("events")] IReadOnlyList<JournalEventResponse> Events);

public sealed record JournalEventResponse(
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("nick")] string Nick,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("objectName")] string? ObjectName);