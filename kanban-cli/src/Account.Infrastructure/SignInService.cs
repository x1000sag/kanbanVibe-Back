using Account.Domain;
using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class SignInService
{
    private readonly AccountDbContext _db;
    private readonly AccountPasswordHasher _hasher;
    private readonly AccessTokenIssuer _tokenIssuer;
    private readonly AuditWriter _audit;
    private readonly TimeProvider _time;

    public SignInService(
        AccountDbContext db,
        AccountPasswordHasher hasher,
        AccessTokenIssuer tokenIssuer,
        AuditWriter audit,
        TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _tokenIssuer = tokenIssuer;
        _audit = audit;
        _time = time;
    }

    public async Task<(string AccessToken, DateTimeOffset ExpiresAt)> SignInAsync(
        string nick,
        string password,
        string source,
        CancellationToken ct)
    {
        string validNick;
        string validPassword;
        try
        {
            (validNick, validPassword) = SignIn.Validate(nick, password);
        }
        catch (AccountRefusalException)
        {
            throw;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var account = TextRules.IsNickCharsetValid(validNick)
                ? await _db.Accounts.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Nick == validNick, ct)
                : null;

            if (account is null)
            {
                _hasher.VerifyDummy(validPassword);
                _audit.StageUnownedSignInFailure(validNick, source);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                throw new AccountRefusalException(RefusalCodes.SignInFailed);
            }

            var passwordOk = _hasher.Verify(validPassword, account.PasswordHash);
            if (!passwordOk)
            {
                _audit.StageOwnedSignIn(account.AccountId, false, source);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                throw new AccountRefusalException(RefusalCodes.SignInFailed);
            }

            var accessId = Guid.NewGuid();
            var issuedAt = AccessLifetime.TruncateToWholeSecondUtc(_time.GetUtcNow());
            _db.AccessRecords.Add(new AccessEntity
            {
                AccessId = accessId,
                AccountId = account.AccountId,
                IssuedAt = issuedAt,
                EndedAt = null,
            });

            _audit.StageOwnedSignIn(account.AccountId, true, source);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var token = _tokenIssuer.Issue(account.AccountId, accessId, issuedAt);
            return (token, AccessTokenIssuer.ExpiresAtFromIssued(issuedAt));
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