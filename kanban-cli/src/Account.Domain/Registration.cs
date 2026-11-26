namespace Account.Domain;

public static class Registration
{
    public static (string Nick, string Password) Validate(string nick, string password)
    {
        var trimmedNick = TextRules.TrimEdgeWhitespace(nick ?? string.Empty);
        if (!TextRules.IsNickLengthValid(trimmedNick) || !TextRules.IsNickCharsetValid(trimmedNick))
        {
            throw new AccountRefusalException(RefusalCodes.NickInvalid);
        }

        var pwd = password ?? string.Empty;
        if (!TextRules.IsPasswordLengthValid(pwd))
        {
            throw new AccountRefusalException(RefusalCodes.PasswordInvalid);
        }

        return (trimmedNick, pwd);
    }
}