using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class AccessGuard
{
    private readonly AccountDbContext _db;
    private readonly TimeProvider _time;
    private readonly AuditWriter _audit;

    public AccessGuard(AccountDbContext db, TimeProvider time, AuditWriter audit)
    {
        _db = db;
        _time = time;
        _audit = audit;
    }

    public async Task<ActiveAccess> RequireActiveAccessAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti);

        if (sub is null || jti is null
            || !Guid.TryParse(sub, out var accountId)
            || !Guid.TryParse(jti, out var accessId))
        {
            throw new AccountRefusalException(RefusalCodes.NoActiveAccess);
        }

        try
        {
            var access = await _db.AccessRecords
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.AccessId == accessId && a.AccountId == accountId, ct);

            if (access is null || !AccessLifetime.IsAlive(access.IssuedAt, access.EndedAt, _time))
            {
                throw new AccountRefusalException(RefusalCodes.NoActiveAccess);
            }

            return new ActiveAccess
            {
                AccountId = access.AccountId,
                Nick = access.Account.Nick,
                AccessId = access.AccessId,
            };
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

    public void EnsurePathNick(ActiveAccess access, string pathNick)
    {
        if (access.Nick != pathNick)
        {
            throw new AccountRefusalException(RefusalCodes.ForeignData);
        }
    }

    public async Task DenyForeignAndLogAsync(ActiveAccess access, CancellationToken ct)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _audit.StageOwned(
                access.AccountId,
                AuditEventTypes.ForeignAccessDenied,
                AuditOutcomes.Failure,
                null,
                null);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception)
        {
            throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
        }

        throw new AccountRefusalException(RefusalCodes.ForeignData);
    }
}