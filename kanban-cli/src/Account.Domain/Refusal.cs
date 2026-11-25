namespace Account.Domain;

public static class RefusalCodes
{
    public const string NickTaken = "nick_taken";
    public const string NickInvalid = "nick_invalid";
    public const string PasswordInvalid = "password_invalid";
    public const string SignInFailed = "sign_in_failed";
    public const string NoActiveAccess = "no_active_access";
    public const string ForeignData = "foreign_data";
    public const string DisplayNameInvalid = "display_name_invalid";
    public const string PasswordChangeRejected = "password_change_rejected";
    public const string StoreUnavailable = "store_unavailable";
    public const string RequestInvalid = "request_invalid";
    public const string CardInvalid = "card_invalid";
    public const string ObjectMissing = "object_missing";
}

public sealed class AccountRefusalException : Exception
{
    public AccountRefusalException(string code)
        : base(code)
    {
        Code = code;
    }

    public string Code { get; }

    public int StatusCode => RefusalHttpStatus.ForCode(Code);
}

public static class RefusalHttpStatus
{
    public static int ForCode(string code) => code switch
    {
        RefusalCodes.NickTaken => 409,
        RefusalCodes.NickInvalid => 400,
        RefusalCodes.PasswordInvalid => 400,
        RefusalCodes.SignInFailed => 401,
        RefusalCodes.NoActiveAccess => 401,
        RefusalCodes.ForeignData => 403,
        RefusalCodes.DisplayNameInvalid => 400,
        RefusalCodes.PasswordChangeRejected => 403,
        RefusalCodes.StoreUnavailable => 503,
        RefusalCodes.RequestInvalid => 400,
        RefusalCodes.CardInvalid => 400,
        RefusalCodes.ObjectMissing => 404,
        _ => 400
    };
}