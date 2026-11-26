namespace Account.Infrastructure.Persistence;

public sealed class CardTagEntity
{
    public Guid CardId { get; set; }
    public string Tag { get; set; } = string.Empty;

    public CardEntity Card { get; set; } = null!;
}