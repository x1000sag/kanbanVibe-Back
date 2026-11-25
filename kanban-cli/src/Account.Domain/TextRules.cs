using System.Text;

namespace Account.Domain;

public static class TextRules
{
    public const int NickMin = 1;
    public const int NickMax = 64;
    public const int DisplayNameMin = 1;
    public const int DisplayNameMax = 128;
    public const int PasswordMin = 1;
    public const int PasswordMax = 1024;

    public static int RuneCount(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    public static string TrimEdgeWhitespace(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return value[start..(end + 1)];
    }

    public static bool IsNickLengthValid(string trimmedNick) =>
        RuneCount(trimmedNick) is >= NickMin and <= NickMax;

    public static bool IsDisplayNameLengthValid(string trimmedDisplayName) =>
        RuneCount(trimmedDisplayName) is >= DisplayNameMin and <= DisplayNameMax;

    public static bool IsPasswordLengthValid(string password) =>
        RuneCount(password) is >= PasswordMin and <= PasswordMax;
}