extern alias KanbanHost;

using Account.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kanban.Tests.Account;

public sealed class AccountApiFactory : WebApplicationFactory<KanbanHost::Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres-test-root")
        .Build();

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.Parse("2026-11-01T12:00:00Z"));

    public string MigratorConnection { get; private set; } = string.Empty;
    public string AppConnection { get; private set; } = string.Empty;
    public string OperatorConnection { get; private set; } = string.Empty;

    private string MigratorPassword { get; set; } = string.Empty;
    private string AppPassword { get; set; } = string.Empty;
    private string OperatorPassword { get; set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        MigratorPassword = Guid.NewGuid().ToString("N");
        AppPassword = Guid.NewGuid().ToString("N");
        OperatorPassword = Guid.NewGuid().ToString("N");

        var host = _postgres.Hostname;
        var port = _postgres.GetMappedPublicPort(5432);

        await using (var conn = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                CREATE DATABASE kanban_account;
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var adminConn = $"Host={host};Port={port};Database=kanban_account;Username=postgres;Password=postgres-test-root";
        await using (var conn = new NpgsqlConnection(adminConn))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                CREATE ROLE kanban_migrator LOGIN PASSWORD '{MigratorPassword}';
                CREATE ROLE kanban_app LOGIN PASSWORD '{AppPassword}';
                CREATE ROLE kanban_operator LOGIN PASSWORD '{OperatorPassword}';
                GRANT CONNECT ON DATABASE kanban_account TO kanban_migrator, kanban_app, kanban_operator;
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        MigratorConnection = $"Host={host};Port={port};Database=kanban_account;Username=kanban_migrator;Password={MigratorPassword}";
        AppConnection = $"Host={host};Port={port};Database=kanban_account;Username=kanban_app;Password={AppPassword}";
        OperatorConnection = $"Host={host};Port={port};Database=kanban_account;Username=kanban_operator;Password={OperatorPassword}";

        Environment.SetEnvironmentVariable("KANBAN_ACCOUNT_CONNECTION", AppConnection);
        Environment.SetEnvironmentVariable("KANBAN_MIGRATOR_CONNECTION", MigratorConnection);
        Environment.SetEnvironmentVariable("KANBAN_OPERATOR_CONNECTION", OperatorConnection);
        Environment.SetEnvironmentVariable("KANBAN_JWT_SIGNING_KEY", "test-signing-key-32-bytes-minimum!");

        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseNpgsql(MigratorConnection)
            .Options;
        await using var db = new AccountDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task StopDatabaseAsync()
    {
        await _postgres.StopAsync();
    }

    public async Task RestartDatabaseAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utcNow = start;

    public void Advance(TimeSpan delta) => _utcNow += delta;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}