using Account.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Kanban.Tests.Account;

[Collection("AccountDb")]
public class MigrationRoleTests(AccountApiFactory factory)
{
    [Fact]
    public async Task AppRole_CannotSelectUnownedAudit()
    {
        await using var conn = new NpgsqlConnection(factory.AppConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM unowned_audit_event LIMIT 1";
        await Assert.ThrowsAsync<PostgresException>(async () => await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task OperatorRole_CanSelectUnownedAudit()
    {
        await using var conn = new NpgsqlConnection(factory.OperatorConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM unowned_audit_event";
        var count = await cmd.ExecuteScalarAsync();
        Assert.NotNull(count);
    }

    [Fact]
    public async Task DownRemovesAccountTables()
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseNpgsql(factory.MigratorConnection)
            .Options;
        await using var db = new AccountDbContext(options);
        await db.Database.MigrateAsync("0");
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var conn = new NpgsqlConnection(factory.AppConnection);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM account LIMIT 1";
            await cmd.ExecuteScalarAsync();
        });
        await db.Database.MigrateAsync();
    }
}