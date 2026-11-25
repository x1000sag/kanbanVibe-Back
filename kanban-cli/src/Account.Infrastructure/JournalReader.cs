using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed record JournalEventDto(
    DateTimeOffset OccurredAt,
    string Nick,
    string Type,
    string Outcome,
    string? Source,
    string? ObjectName);

public sealed record JournalDto(IReadOnlyList<JournalEventDto> Events);

public sealed class JournalReader
{
    private readonly AccountDbContext _db;

    public JournalReader(AccountDbContext db) => _db = db;

    public async Task<JournalDto> ReadAsync(Guid accountId, string nick, CancellationToken ct)
    {
        try
        {
            var events = await _db.AccountAuditEvents.AsNoTracking()
                .Where(e => e.AccountId == accountId)
                .OrderBy(e => e.OccurredAt)
                .ThenBy(e => e.EventId)
                .ToListAsync(ct);

            var dtos = events.Select(e => new JournalEventDto(
                e.OccurredAt,
                nick,
                e.EventType,
                e.Outcome,
                e.Source,
                e.ObjectName)).ToList();

            return new JournalDto(dtos);
        }
        catch (AccountRefusalException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
        }
    }
}