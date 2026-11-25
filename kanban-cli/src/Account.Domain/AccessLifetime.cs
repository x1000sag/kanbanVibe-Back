namespace Account.Domain;

public static class AccessLifetime
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

    public static DateTimeOffset TruncateToWholeSecondUtc(DateTimeOffset instant) =>
        new(instant.UtcDateTime.AddTicks(-(instant.Ticks % TimeSpan.TicksPerSecond)), TimeSpan.Zero);

    public static bool IsAlive(DateTimeOffset issuedAt, DateTimeOffset? endedAt, TimeProvider timeProvider)
    {
        if (endedAt is not null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var issued = TruncateToWholeSecondUtc(issuedAt);
        return now < issued + MaxDuration;
    }

    public static DateTimeOffset ExpiresAt(DateTimeOffset issuedAt)
    {
        var issued = TruncateToWholeSecondUtc(issuedAt);
        return issued + MaxDuration;
    }
}