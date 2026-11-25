using Account.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Account.Infrastructure;

public static class StoreErrors
{
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static AccountRefusalException ToRefusal(Exception ex)
    {
        if (ex is AccountRefusalException refusal)
        {
            return refusal;
        }

        if (ex is DbUpdateException dbEx && IsUniqueViolation(dbEx))
        {
            return new AccountRefusalException(RefusalCodes.NickTaken);
        }

        if (ex is PostgresException or DbUpdateException or InvalidOperationException
            or TimeoutException or NpgsqlException)
        {
            return new AccountRefusalException(RefusalCodes.StoreUnavailable);
        }

        return new AccountRefusalException(RefusalCodes.StoreUnavailable);
    }
}