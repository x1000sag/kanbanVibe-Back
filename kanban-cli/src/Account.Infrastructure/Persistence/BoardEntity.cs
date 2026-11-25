namespace Account.Infrastructure.Persistence;

public sealed class BoardEntity
{
    public Guid BoardId { get; set; }
    public Guid AccountId { get; set; }

    public AccountEntity Account { get; set; } = null!;
    public ICollection<ColumnEntity> Columns { get; set; } = new List<ColumnEntity>();
}