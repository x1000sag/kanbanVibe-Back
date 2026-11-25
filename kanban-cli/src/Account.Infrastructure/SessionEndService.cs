using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class SessionEndService
{
    private readonly AccountDbContext _db;
    private readonly AuditWriter _audit;
    private readonly TimeProvider _time;

    public SessionEndService(AccountDbContext db, AuditWriter audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    public async Task EndAccessAsync(Guid accountId, Guid accessId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var access = await _db.AccessRecords
                .FirstOrDefaultAsync(a => a.AccessId == accessId && a.AccountId == accountId, ct);
            if (access is null)
            {
                throw new AccountRefusalException(RefusalCodes.NoActiveAccess);
            }

            if (access.EndedAt is null)
            {
                access.EndedAt = AccessLifetime.TruncateToWholeSecondUtc(_time.GetUtcNow());
            }

            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.AccessEnd,
                AuditOutcomes.Success);

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