namespace Account.Infrastructure.Persistence;

public sealed class AccessEntity
{
    public Guid AccessId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public AccountEntity Account { get; set; } = null!;
}