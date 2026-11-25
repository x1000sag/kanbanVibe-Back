namespace Account.Infrastructure.Persistence;

public sealed class UnownedAuditEventEntity
{
    public Guid EventId { get; set; }
    public string AttemptedNick { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}