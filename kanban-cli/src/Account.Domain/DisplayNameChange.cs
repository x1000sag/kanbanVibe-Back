namespace Account.Domain;

public static class DisplayNameChange
{
    public static string Validate(string displayName)
    {
        var trimmed = TextRules.TrimEdgeWhitespace(displayName ?? string.Empty);
        if (!TextRules.IsDisplayNameLengthValid(trimmed))
        {
            throw new AccountRefusalException(RefusalCodes.DisplayNameInvalid);
        }

        return trimmed;
    }
}