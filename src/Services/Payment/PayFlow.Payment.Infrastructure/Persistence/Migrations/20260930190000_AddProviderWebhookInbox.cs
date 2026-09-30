using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Payment.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Payment.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260930190000_AddProviderWebhookInbox")]
public partial class AddProviderWebhookInbox : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "provider_webhook_inbox",
            columns: table => new
            {
                event_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                event_type = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                operation_type = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                payment_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                refund_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: true),
                outcome = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                provider_reference = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true),
                error_code = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                occurred_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                received_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                payload_hash = table.Column<string>(
                    type: "character(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_provider_webhook_inbox",
                    x => x.event_id);

                table.CheckConstraint(
                    "ck_provider_webhook_inbox_payload_hash_length",
                    "char_length(payload_hash) = 64");
            });

        migrationBuilder.CreateIndex(
            name: "IX_provider_webhook_inbox_operation_payment",
            table: "provider_webhook_inbox",
            columns: new[]
            {
                "operation_type",
                "payment_id"
            });

        migrationBuilder.CreateIndex(
            name: "IX_provider_webhook_inbox_unprocessed",
            table: "provider_webhook_inbox",
            column: "processed_at_utc");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "provider_webhook_inbox");
    }
}
