using System.Text;

namespace Kanban.Api.Account;

public sealed class AccountOptions
{
    public string AccountConnection { get; }
    public string MigratorConnection { get; }
    public string OperatorConnection { get; }
    public string JwtSigningKey { get; }

    public AccountOptions()
    {
        AccountConnection = RequireEnv("KANBAN_ACCOUNT_CONNECTION");
        MigratorConnection = RequireEnv("KANBAN_MIGRATOR_CONNECTION");
        OperatorConnection = RequireEnv("KANBAN_OPERATOR_CONNECTION");
        JwtSigningKey = RequireSigningKey("KANBAN_JWT_SIGNING_KEY");
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Переменная окружения {name} не задана.");
        }

        return value;
    }

    private static string RequireSigningKey(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Переменная окружения {name} не задана.");
        }

        if (Encoding.UTF8.GetByteCount(value) < 32)
        {
            throw new InvalidOperationException($"Переменная окружения {name} короче 32 байт.");
        }

        return value;
    }
}