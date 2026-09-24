using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260925213000_InitialSagaInbox")]
public partial class InitialSagaInbox : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inbox_messages",
            columns: table => new
            {
                consumer_name = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
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
                source_topic = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false),
                source_partition = table.Column<int>(
                    type: "integer",
                    nullable: false),
                source_offset = table.Column<long>(
                    type: "bigint",
                    nullable: false),
                received_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_inbox_messages",
                    x => new
                    {
                        x.consumer_name,
                        x.message_id
                    });

                table.CheckConstraint(
                    "ck_inbox_messages_schema_version_positive",
                    "schema_version > 0");

                table.CheckConstraint(
                    "ck_inbox_messages_source_offset_non_negative",
                    "source_offset >= 0");

                table.CheckConstraint(
                    "ck_inbox_messages_source_partition_non_negative",
                    "source_partition >= 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_processed_at_utc",
            table: "inbox_messages",
            column: "processed_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_source_position",
            table: "inbox_messages",
            columns: new[]
            {
                "source_topic",
                "source_partition",
                "source_offset"
            });
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inbox_messages");
    }
}
