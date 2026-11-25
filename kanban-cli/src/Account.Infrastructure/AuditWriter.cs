using Account.Domain;
using Account.Infrastructure.Persistence;

namespace Account.Infrastructure;

public sealed class AuditWriter(AccountDbContext db, TimeProvider timeProvider)
{
    public void StageOwnedSignIn(Guid accountId, bool success, string source)
    {
        StageOwned(accountId, AuditEventTypes.SignIn, success ? AuditOutcomes.Success : AuditOutcomes.Failure, source, null);
    }

    public void StageUnownedSignInFailure(string attemptedNick, string source)
    {
        db.UnownedAuditEvents.Add(new UnownedAuditEventEntity
        {
            EventId = Guid.NewGuid(),
            AttemptedNick = attemptedNick,
            OccurredAt = AccessLifetime.TruncateToWholeSecondUtc(timeProvider.GetUtcNow()),
            EventType = AuditEventTypes.SignIn,
            Outcome = AuditOutcomes.Failure,
            Source = source,
        });
    }

    public void StageOwned(
        Guid accountId,
        string eventType,
        string outcome,
        string? source,
        string? objectName)
    {
        db.AccountAuditEvents.Add(new AccountAuditEventEntity
        {
            EventId = Guid.NewGuid(),
            AccountId = accountId,
            OccurredAt = AccessLifetime.TruncateToWholeSecondUtc(timeProvider.GetUtcNow()),
            EventType = eventType,
            Outcome = outcome,
            Source = source,
            ObjectName = objectName,
        });
    }

    public void CreateOwnerEvent(
        Guid accountId,
        DateTimeOffset _,
        string eventType,
        string outcome,
        string? source = null,
        string? objectName = null) =>
        StageOwned(accountId, eventType, outcome, source, objectName);
}