using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Account.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccountStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nick = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "unowned_audit_event",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_nick = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unowned_audit_event", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "access",
                columns: table => new
                {
                    access_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access", x => x.access_id);
                    table.ForeignKey(
                        name: "FK_access_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_audit_event",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    object_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_audit_event", x => x.event_id);
                    table.ForeignKey(
                        name: "FK_account_audit_event_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "board",
                columns: table => new
                {
                    board_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board", x => x.board_id);
                    table.ForeignKey(
                        name: "FK_board_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "column",
                columns: table => new
                {
                    column_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_column", x => x.column_id);
                    table.ForeignKey(
                        name: "FK_column_board_board_id",
                        column: x => x.board_id,
                        principalTable: "board",
                        principalColumn: "board_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "card",
                columns: table => new
                {
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card", x => x.card_id);
                    table.ForeignKey(
                        name: "FK_card_column_column_id",
                        column: x => x.column_id,
                        principalTable: "column",
                        principalColumn: "column_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_tag",
                columns: table => new
                {
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_tag", x => new { x.card_id, x.tag });
                    table.ForeignKey(
                        name: "FK_card_tag_card_card_id",
                        column: x => x.card_id,
                        principalTable: "card",
                        principalColumn: "card_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_account_id",
                table: "access",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_nick",
                table: "account",
                column: "nick",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_audit_event_account_id",
                table: "account_audit_event",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_board_account_id",
                table: "board",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_column_id",
                table: "card",
                column: "column_id");

            migrationBuilder.CreateIndex(
                name: "IX_column_board_id_name",
                table: "column",
                columns: new[] { "board_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_column_board_id_position",
                table: "column",
                columns: new[] { "board_id", "position" },
                unique: true);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_account_nick";
                CREATE UNIQUE INDEX "IX_account_nick" ON account (nick COLLATE "C");

                CREATE UNIQUE INDEX "IX_column_board_id_lower_name" ON "column" (board_id, lower(name));
                CREATE UNIQUE INDEX "IX_card_tag_card_id_lower_tag" ON card_tag (card_id, lower(tag));

                ALTER TABLE account ADD CONSTRAINT account_nick_length CHECK (char_length(nick) BETWEEN 1 AND 64);
                ALTER TABLE account ADD CONSTRAINT account_nick_trim CHECK (nick = trim(nick));
                ALTER TABLE account ADD CONSTRAINT account_display_name_length CHECK (char_length(display_name) BETWEEN 1 AND 128);
                ALTER TABLE account ADD CONSTRAINT account_display_name_trim CHECK (display_name = trim(display_name));
                ALTER TABLE account ADD CONSTRAINT account_password_hash_nonempty CHECK (password_hash <> '');

                ALTER TABLE account_audit_event ADD CONSTRAINT account_audit_source_sign_in CHECK (
                  (event_type = 'sign_in' AND source IS NOT NULL AND char_length(source) BETWEEN 1 AND 45)
                  OR (event_type <> 'sign_in' AND source IS NULL));

                ALTER TABLE account_audit_event ADD CONSTRAINT account_audit_object_name_delete CHECK (
                  (event_type IN ('column_deleted', 'card_deleted') AND object_name IS NOT NULL)
                  OR (event_type NOT IN ('column_deleted', 'card_deleted') AND object_name IS NULL));

                ALTER TABLE unowned_audit_event ADD CONSTRAINT unowned_event_type CHECK (event_type = 'sign_in');
                ALTER TABLE unowned_audit_event ADD CONSTRAINT unowned_outcome CHECK (outcome = 'failure');
                ALTER TABLE unowned_audit_event ADD CONSTRAINT unowned_source_length CHECK (char_length(source) BETWEEN 1 AND 45);
                ALTER TABLE unowned_audit_event ADD CONSTRAINT unowned_attempted_nick_length CHECK (char_length(attempted_nick) BETWEEN 1 AND 64);

                CREATE OR REPLACE FUNCTION account_reject_nick_update() RETURNS trigger AS $$
                BEGIN
                  IF NEW.nick IS DISTINCT FROM OLD.nick THEN
                    RAISE EXCEPTION 'nick_immutable';
                  END IF;
                  RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER account_nick_immutable
                  BEFORE UPDATE ON account
                  FOR EACH ROW EXECUTE FUNCTION account_reject_nick_update();

                CREATE OR REPLACE FUNCTION account_reject_delete() RETURNS trigger AS $$
                BEGIN
                  RAISE EXCEPTION 'account_delete_forbidden';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER account_delete_forbidden
                  BEFORE DELETE ON account
                  FOR EACH ROW EXECUTE FUNCTION account_reject_delete();

                CREATE OR REPLACE FUNCTION audit_reject_mutation() RETURNS trigger AS $$
                BEGIN
                  RAISE EXCEPTION 'audit_immutable';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER account_audit_immutable
                  BEFORE UPDATE OR DELETE ON account_audit_event
                  FOR EACH ROW EXECUTE FUNCTION audit_reject_mutation();

                CREATE TRIGGER unowned_audit_immutable
                  BEFORE UPDATE OR DELETE ON unowned_audit_event
                  FOR EACH ROW EXECUTE FUNCTION audit_reject_mutation();

                ALTER TABLE account OWNER TO kanban_migrator;
                ALTER TABLE board OWNER TO kanban_migrator;
                ALTER TABLE "column" OWNER TO kanban_migrator;
                ALTER TABLE card OWNER TO kanban_migrator;
                ALTER TABLE card_tag OWNER TO kanban_migrator;
                ALTER TABLE access OWNER TO kanban_migrator;
                ALTER TABLE account_audit_event OWNER TO kanban_migrator;
                ALTER TABLE unowned_audit_event OWNER TO kanban_migrator;

                GRANT SELECT, INSERT, UPDATE, DELETE ON account TO kanban_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON board TO kanban_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON "column" TO kanban_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON card TO kanban_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON card_tag TO kanban_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON access TO kanban_app;
                GRANT SELECT, INSERT ON account_audit_event TO kanban_app;
                GRANT INSERT ON unowned_audit_event TO kanban_app;
                GRANT SELECT ON unowned_audit_event TO kanban_operator;
                REVOKE ALL ON unowned_audit_event FROM kanban_app;
                GRANT INSERT ON unowned_audit_event TO kanban_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access");

            migrationBuilder.DropTable(
                name: "account_audit_event");

            migrationBuilder.DropTable(
                name: "card_tag");

            migrationBuilder.DropTable(
                name: "unowned_audit_event");

            migrationBuilder.DropTable(
                name: "card");

            migrationBuilder.DropTable(
                name: "column");

            migrationBuilder.DropTable(
                name: "board");

            migrationBuilder.DropTable(
                name: "account");
        }
    }
}
