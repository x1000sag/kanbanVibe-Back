namespace Account.Domain;

public static class PasswordChange
{
    public static string ValidateNew(string newPassword)
    {
        var pwd = newPassword ?? string.Empty;
        if (!TextRules.IsPasswordLengthValid(pwd))
        {
            throw new AccountRefusalException(RefusalCodes.PasswordInvalid);
        }

        return pwd;
    }

    public static void RejectCurrentMismatch()
    {
        throw new AccountRefusalException(RefusalCodes.PasswordChangeRejected);
    }
}