namespace Account.Infrastructure.Persistence;

public sealed class CardEntity
{
    public Guid CardId { get; set; }
    public Guid ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ColumnEntity Column { get; set; } = null!;
    public ICollection<CardTagEntity> Tags { get; set; } = new List<CardTagEntity>();
}