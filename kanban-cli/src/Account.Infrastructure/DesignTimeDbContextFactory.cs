using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Account.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AccountDbContext>
{
    public AccountDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("KANBAN_MIGRATOR_CONNECTION")
            ?? "Host=localhost;Database=kanban_account;Username=kanban_migrator;Password=design";

        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new AccountDbContext(options);
    }
}