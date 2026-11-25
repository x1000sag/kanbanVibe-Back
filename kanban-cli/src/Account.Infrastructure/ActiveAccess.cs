namespace Account.Infrastructure;

public sealed class ActiveAccess
{
    public required Guid AccountId { get; init; }
    public required string Nick { get; init; }
    public required Guid AccessId { get; init; }
}