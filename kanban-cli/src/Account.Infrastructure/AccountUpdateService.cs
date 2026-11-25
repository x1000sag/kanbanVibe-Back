using Account.Domain;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure;

public sealed class AccountUpdateService
{
    private readonly AccountDbContext _db;
    private readonly AccountPasswordHasher _hasher;
    private readonly AuditWriter _audit;
    private readonly TimeProvider _time;

    public AccountUpdateService(
        AccountDbContext db,
        AccountPasswordHasher hasher,
        AuditWriter audit,
        TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
        _time = time;
    }

    public async Task<(string Nick, string DisplayName)> ChangeDisplayNameAsync(
        Guid accountId,
        string displayName,
        CancellationToken ct)
    {
        var validName = DisplayNameChange.Validate(displayName);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            account.DisplayName = validName;
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.DisplayNameChange,
                AuditOutcomes.Success);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (account.Nick, validName);
        }
        catch (AccountRefusalException ex) when (ex.Code == RefusalCodes.DisplayNameInvalid)
        {
            await tx.RollbackAsync(ct);
            await TryLogDisplayNameFailureAsync(accountId, ct);
            throw;
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

    private async Task TryLogDisplayNameFailureAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.DisplayNameChange,
                AuditOutcomes.Failure);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            throw StoreErrors.ToRefusal(ex);
        }
    }

    public async Task ChangePasswordAsync(
        Guid accountId,
        string currentPassword,
        string newPassword,
        CancellationToken ct)
    {
        string validNew;
        try
        {
            validNew = PasswordChange.ValidateNew(newPassword);
        }
        catch (AccountRefusalException ex) when (ex.Code == RefusalCodes.PasswordInvalid)
        {
            await LogPasswordFailureAsync(accountId, ct);
            throw;
        }

        var current = currentPassword ?? string.Empty;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null)
            {
                throw new AccountRefusalException(RefusalCodes.StoreUnavailable);
            }

            if (!_hasher.Verify(current, account.PasswordHash))
            {
                await tx.RollbackAsync(ct);
                await LogPasswordFailureAsync(accountId, ct);
                PasswordChange.RejectCurrentMismatch();
            }

            account.PasswordHash = _hasher.Hash(validNew);
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.PasswordChange,
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

    private async Task LogPasswordFailureAsync(Guid accountId, CancellationToken ct)
    {
        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _audit.CreateOwnerEvent(
                accountId,
                _time.GetUtcNow(),
                AuditEventTypes.PasswordChange,
                AuditOutcomes.Failure);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            throw StoreErrors.ToRefusal(ex);
        }
    }
}