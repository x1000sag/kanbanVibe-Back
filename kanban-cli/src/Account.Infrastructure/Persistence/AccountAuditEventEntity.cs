namespace Account.Infrastructure.Persistence;

public sealed class AccountAuditEventEntity
{
    public Guid EventId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? ObjectName { get; set; }

    public AccountEntity Account { get; set; } = null!;
}