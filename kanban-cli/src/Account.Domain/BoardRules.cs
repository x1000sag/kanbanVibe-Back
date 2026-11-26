using System.Globalization;

namespace Account.Domain;

public static class BoardRules
{
    public static void ValidateCard(string title, string? deadline, IReadOnlyList<string>? tags)
    {
        if (string.IsNullOrEmpty(title))
        {
            throw new AccountRefusalException(RefusalCodes.CardInvalid);
        }

        if (deadline is not null && !DateOnly.TryParseExact(deadline, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new AccountRefusalException(RefusalCodes.CardInvalid);
        }

        if (tags is not null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    throw new AccountRefusalException(RefusalCodes.CardInvalid);
                }

                if (!seen.Add(tag))
                {
                    throw new AccountRefusalException(RefusalCodes.CardInvalid);
                }
            }
        }
    }

    public static DateOnly? ParseDeadline(string? deadline)
    {
        if (deadline is null)
        {
            return null;
        }

        return DateOnly.ParseExact(deadline, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}