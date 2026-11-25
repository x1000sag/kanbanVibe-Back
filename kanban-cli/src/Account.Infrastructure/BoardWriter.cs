using Account.Domain;
using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class BoardWriter
{
    private readonly AccountDbContext _db;
    private readonly TimeProvider _time;
    private readonly AuditWriter _audit;

    public BoardWriter(AccountDbContext db, TimeProvider time, AuditWriter audit)
    {
        _db = db;
        _time = time;
        _audit = audit;
    }

    public async Task<ProfileCardDto> CreateCardAsync(
        Guid accountId,
        string columnName,
        string title,
        string? description,
        IReadOnlyList<string>? tags,
        string? deadline,
        CancellationToken ct)
    {
        BoardRules.ValidateCard(title, deadline, tags?.ToList());

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var board = await _db.Boards.AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountId == accountId, ct);
            if (board is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            var columns = await _db.Columns
                .Where(c => c.BoardId == board.BoardId)
                .ToListAsync(ct);

            var column = columns.FirstOrDefault(c =>
                string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));

            if (column is null)
            {
                throw new AccountRefusalException(RefusalCodes.ObjectMissing);
            }

            var cardId = Guid.NewGuid();
            var createdAt = new DateTimeOffset(
                _time.GetUtcNow().UtcDateTime.AddTicks(-(_time.GetUtcNow().Ticks % TimeSpan.TicksPerSecond)),
                TimeSpan.Zero);

            var card = new CardEntity
            {
                CardId = cardId,
                ColumnId = column.ColumnId,
                Title = title,
                Description = description,
                Deadline = BoardRules.ParseDeadline(deadline),
                CreatedAt = createdAt,
            };
            _db.Cards.Add(card);

            if (tags is not null)
            {
                foreach (var tag in tags)
                {
                    _db.CardTags.Add(new CardTagEntity { CardId = cardId, Tag = tag });
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new ProfileCardDto(
                cardId,
                title,
                description,
                tags is null ? [] : tags.ToList(),
                deadline,
                createdAt);
        }
        catch (AccountRefusalException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw StoreErrors.ToRefusal(ex);
        }
    }

    public async Task DeleteCardAsync(Guid accountId, Guid cardId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var card = await _db.Cards
                .Include(c => c.Column)
                .ThenInclude(col => col.Board)
                .FirstOrDefaultAsync(c => c.CardId == cardId, ct);

            if (card is null)
            {
                throw new AccountRefusalException(RefusalCodes.ObjectMissing);
            }

            if (card.Column.Board.AccountId != accountId)
            {
                throw new AccountRefusalException(RefusalCodes.ForeignData);
            }

            var title = card.Title;
            _db.Cards.Remove(card);
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.CardDeleted,
                AuditOutcomes.Success,
                objectName: title);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (AccountRefusalException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw StoreErrors.ToRefusal(ex);
        }
    }

    public async Task DeleteColumnAsync(Guid accountId, string columnName, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var board = await _db.Boards
                .FirstOrDefaultAsync(b => b.AccountId == accountId, ct);
            if (board is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            var column = await _db.Columns
                .FirstOrDefaultAsync(c => c.BoardId == board.BoardId && c.Name == columnName, ct);

            if (column is null)
            {
                throw new AccountRefusalException(RefusalCodes.ObjectMissing);
            }

            _db.Columns.Remove(column);
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.ColumnDeleted,
                AuditOutcomes.Success,
                objectName: columnName);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (AccountRefusalException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw StoreErrors.ToRefusal(ex);
        }
    }
}