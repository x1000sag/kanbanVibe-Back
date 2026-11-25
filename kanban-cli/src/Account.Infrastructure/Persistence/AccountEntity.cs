namespace Account.Infrastructure.Persistence;

public sealed class AccountEntity
{
    public Guid AccountId { get; set; }
    public string Nick { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public BoardEntity? Board { get; set; }
    public ICollection<AccessEntity> AccessRecords { get; set; } = new List<AccessEntity>();
    public ICollection<AccountAuditEventEntity> AuditEvents { get; set; } = new List<AccountAuditEventEntity>();
}