using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260926143000_AddSagaOutbox")]
public partial class AddSagaOutbox : Migration
{
    private static readonly string[] PendingPublishIndexColumns =
    [
        "published_at_utc",
        "next_attempt_at_utc",
        "created_at_utc"
    ];

    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                outbox_message_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                message_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                message_type = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false),
                schema_version = table.Column<int>(
                    type: "integer",
                    nullable: false),
                aggregate_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                correlation_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                causation_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),
                occurred_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                destination = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false),
                producer = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                trace_parent = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true),
                payload = table.Column<string>(
                    type: "jsonb",
                    nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                published_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                attempt_count = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 0),
                next_attempt_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                last_error_code = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                claim_token = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),
                claimed_until_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_outbox_messages",
                    x => x.outbox_message_id);

                table.CheckConstraint(
                    "ck_outbox_messages_attempt_count_non_negative",
                    "attempt_count >= 0");

                table.CheckConstraint(
                    "ck_outbox_messages_claim_pair",
                    "(claim_token IS NULL AND claimed_until_utc IS NULL) OR "
                    + "(claim_token IS NOT NULL AND claimed_until_utc IS NOT NULL)");

                table.CheckConstraint(
                    "ck_outbox_messages_schema_version_positive",
                    "schema_version > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_message_id",
            table: "outbox_messages",
            column: "message_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_publish_pending",
            table: "outbox_messages",
            columns: PendingPublishIndexColumns,
            filter: "published_at_utc IS NULL");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages");
    }
}
