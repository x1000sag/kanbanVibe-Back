using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Account.Infrastructure;

public sealed class AccountDbContext : DbContext
{
    public AccountDbContext(DbContextOptions<AccountDbContext> options)
        : base(options)
    {
    }

    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<BoardEntity> Boards => Set<BoardEntity>();
    public DbSet<ColumnEntity> Columns => Set<ColumnEntity>();
    public DbSet<CardEntity> Cards => Set<CardEntity>();
    public DbSet<CardTagEntity> CardTags => Set<CardTagEntity>();
    public DbSet<AccessEntity> AccessRecords => Set<AccessEntity>();
    public DbSet<AccountAuditEventEntity> AccountAuditEvents => Set<AccountAuditEventEntity>();
    public DbSet<UnownedAuditEventEntity> UnownedAuditEvents => Set<UnownedAuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<AccountEntity>();
        account.ToTable("account");
        account.HasKey(e => e.AccountId);
        account.Property(e => e.AccountId).HasColumnName("account_id");
        account.Property(e => e.Nick).HasColumnName("nick").HasMaxLength(256).IsRequired();
        account.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(512).IsRequired();
        account.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
        account.HasIndex(e => e.Nick).IsUnique();

        var board = modelBuilder.Entity<BoardEntity>();
        board.ToTable("board");
        board.HasKey(e => e.BoardId);
        board.Property(e => e.BoardId).HasColumnName("board_id");
        board.Property(e => e.AccountId).HasColumnName("account_id");
        board.HasIndex(e => e.AccountId).IsUnique();
        board.HasOne(e => e.Account).WithOne(a => a.Board!)
            .HasForeignKey<BoardEntity>(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        var column = modelBuilder.Entity<ColumnEntity>();
        column.ToTable("column");
        column.HasKey(e => e.ColumnId);
        column.Property(e => e.ColumnId).HasColumnName("column_id");
        column.Property(e => e.BoardId).HasColumnName("board_id");
        column.Property(e => e.Name).HasColumnName("name").IsRequired();
        column.Property(e => e.Position).HasColumnName("position");
        column.HasIndex(e => new { e.BoardId, e.Position }).IsUnique();
        column.HasIndex(e => new { e.BoardId, e.Name }).IsUnique();
        column.HasOne(e => e.Board).WithMany(b => b.Columns)
            .HasForeignKey(e => e.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        var card = modelBuilder.Entity<CardEntity>();
        card.ToTable("card");
        card.HasKey(e => e.CardId);
        card.Property(e => e.CardId).HasColumnName("card_id");
        card.Property(e => e.ColumnId).HasColumnName("column_id");
        card.Property(e => e.Title).HasColumnName("title").IsRequired();
        card.Property(e => e.Description).HasColumnName("description");
        card.Property(e => e.Deadline).HasColumnName("deadline");
        card.Property(e => e.CreatedAt).HasColumnName("created_at");
        card.HasOne(e => e.Column).WithMany(c => c.Cards)
            .HasForeignKey(e => e.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        var cardTag = modelBuilder.Entity<CardTagEntity>();
        cardTag.ToTable("card_tag");
        cardTag.HasKey(e => new { e.CardId, e.Tag });
        cardTag.Property(e => e.CardId).HasColumnName("card_id");
        cardTag.Property(e => e.Tag).HasColumnName("tag").IsRequired();
        cardTag.HasOne(e => e.Card).WithMany(c => c.Tags)
            .HasForeignKey(e => e.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        var access = modelBuilder.Entity<AccessEntity>();
        access.ToTable("access");
        access.HasKey(e => e.AccessId);
        access.Property(e => e.AccessId).HasColumnName("access_id");
        access.Property(e => e.AccountId).HasColumnName("account_id");
        access.Property(e => e.IssuedAt).HasColumnName("issued_at");
        access.Property(e => e.EndedAt).HasColumnName("ended_at");
        access.HasOne(e => e.Account).WithMany(a => a.AccessRecords)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        var ownerAudit = modelBuilder.Entity<AccountAuditEventEntity>();
        ownerAudit.ToTable("account_audit_event");
        ownerAudit.HasKey(e => e.EventId);
        ownerAudit.Property(e => e.EventId).HasColumnName("event_id");
        ownerAudit.Property(e => e.AccountId).HasColumnName("account_id");
        ownerAudit.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        ownerAudit.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        ownerAudit.Property(e => e.Outcome).HasColumnName("outcome").IsRequired();
        ownerAudit.Property(e => e.Source).HasColumnName("source");
        ownerAudit.Property(e => e.ObjectName).HasColumnName("object_name");
        ownerAudit.HasOne(e => e.Account).WithMany(a => a.AuditEvents)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        var unownedAudit = modelBuilder.Entity<UnownedAuditEventEntity>();
        unownedAudit.ToTable("unowned_audit_event");
        unownedAudit.HasKey(e => e.EventId);
        unownedAudit.Property(e => e.EventId).HasColumnName("event_id");
        unownedAudit.Property(e => e.AttemptedNick).HasColumnName("attempted_nick").IsRequired();
        unownedAudit.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        unownedAudit.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        unownedAudit.Property(e => e.Outcome).HasColumnName("outcome").IsRequired();
        unownedAudit.Property(e => e.Source).HasColumnName("source").IsRequired();
    }
}

public static class AccountStoreExtensions
{
    public static IServiceCollection AddAccountStore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AccountDbContext>(options =>
            options.UseNpgsql(connectionString));
        return services;
    }
}