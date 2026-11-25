using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed record ProfileCardDto(
    Guid Id,
    string Title,
    string? Description,
    IReadOnlyList<string> Tags,
    string? Deadline,
    DateTimeOffset CreatedAt);

public sealed record ProfileColumnDto(string Name, IReadOnlyList<ProfileCardDto> Cards);

public sealed record ProfileDto(string Nick, string DisplayName, IReadOnlyList<ProfileColumnDto> Columns);

public sealed class ProfileReader
{
    private readonly AccountDbContext _db;

    public ProfileReader(AccountDbContext db) => _db = db;

    public async Task<ProfileDto> ReadAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            var account = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            var board = await _db.Boards.AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountId == accountId, ct);
            if (board is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            var columns = await _db.Columns.AsNoTracking()
                .Where(c => c.BoardId == board.BoardId)
                .OrderBy(c => c.Position)
                .ToListAsync(ct);

            var columnIds = columns.Select(c => c.ColumnId).ToList();
            var cards = await _db.Cards.AsNoTracking()
                .Where(c => columnIds.Contains(c.ColumnId))
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.CardId)
                .ToListAsync(ct);

            var cardIds = cards.Select(c => c.CardId).ToList();
            var tags = await _db.CardTags.AsNoTracking()
                .Where(t => cardIds.Contains(t.CardId))
                .OrderBy(t => t.Tag)
                .ToListAsync(ct);

            var tagsByCard = tags.GroupBy(t => t.CardId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Tag).ToList());

            var cardsByColumn = cards.GroupBy(c => c.ColumnId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var columnDtos = columns.Select(col =>
            {
                var colCards = cardsByColumn.GetValueOrDefault(col.ColumnId) ?? [];
                var cardDtos = colCards.Select(card => new ProfileCardDto(
                    card.CardId,
                    card.Title,
                    card.Description,
                    tagsByCard.GetValueOrDefault(card.CardId) ?? Array.Empty<string>(),
                    card.Deadline?.ToString("yyyy-MM-dd"),
                    card.CreatedAt)).ToList();
                return new ProfileColumnDto(col.Name, cardDtos);
            }).ToList();

            return new ProfileDto(account.Nick, account.DisplayName, columnDtos);
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