namespace Account.Infrastructure.Persistence;

public sealed class ColumnEntity
{
    public Guid ColumnId { get; set; }
    public Guid BoardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public short Position { get; set; }

    public BoardEntity Board { get; set; } = null!;
    public ICollection<CardEntity> Cards { get; set; } = new List<CardEntity>();
}