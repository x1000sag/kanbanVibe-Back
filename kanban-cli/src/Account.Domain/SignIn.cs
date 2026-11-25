namespace Account.Domain;

public static class SignIn
{
    public static (string Nick, string Password) Validate(string nick, string password)
    {
        var trimmedNick = TextRules.TrimEdgeWhitespace(nick ?? string.Empty);
        var pwd = password ?? string.Empty;

        if (!TextRules.IsNickLengthValid(trimmedNick) || !TextRules.IsPasswordLengthValid(pwd))
        {
            throw new AccountRefusalException(RefusalCodes.SignInFailed);
        }

        return (trimmedNick, pwd);
    }
}