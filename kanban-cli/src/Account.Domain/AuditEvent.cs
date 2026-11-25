namespace Account.Domain;

public static class AuditEventTypes
{
    public const string SignIn = "sign_in";
    public const string AccessEnd = "access_end";
    public const string PasswordChange = "password_change";
    public const string DisplayNameChange = "display_name_change";
    public const string ForeignAccessDenied = "foreign_access_denied";
    public const string ColumnDeleted = "column_deleted";
    public const string CardDeleted = "card_deleted";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
}