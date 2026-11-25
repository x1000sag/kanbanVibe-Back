using Account.Domain;
using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class RegistrationService
{
    private readonly AccountDbContext _db;
    private readonly AccountPasswordHasher _hasher;

    public RegistrationService(AccountDbContext db, AccountPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<(string Nick, string DisplayName)> RegisterAsync(string nick, string password, CancellationToken ct)
    {
        var (validNick, validPassword) = Registration.Validate(nick, password);
        var accountId = Guid.NewGuid();
        var boardId = Guid.NewGuid();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Accounts.Add(new AccountEntity
            {
                AccountId = accountId,
                Nick = validNick,
                DisplayName = validNick,
                PasswordHash = _hasher.Hash(validPassword),
            });

            _db.Boards.Add(new BoardEntity { BoardId = boardId, AccountId = accountId });

            var columnNames = new[] { ("ToDo", (short)0), ("InProgress", (short)1), ("Done", (short)2) };
            foreach (var (name, position) in columnNames)
            {
                _db.Columns.Add(new ColumnEntity
                {
                    ColumnId = Guid.NewGuid(),
                    BoardId = boardId,
                    Name = name,
                    Position = position,
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (validNick, validNick);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw StoreErrors.ToRefusal(ex);
        }
    }
}