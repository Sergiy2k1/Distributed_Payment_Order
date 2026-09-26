using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Payment.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260926233000_InitialPaymentPersistence")]
public partial class InitialPaymentPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ledger_transactions",
            columns: table => new
            {
                ledger_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                business_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ledger_transactions", x => x.ledger_transaction_id);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            columns: table => new
            {
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failure_reason_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.payment_id);
                table.CheckConstraint("ck_payments_amount_positive", "amount > 0");
            });

        migrationBuilder.CreateTable(
            name: "provider_operations",
            columns: table => new
            {
                provider_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                business_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                provider_idempotency_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                provider_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_provider_operations", x => x.provider_operation_id);
                table.CheckConstraint("ck_provider_operations_attempt_count_non_negative", "attempt_count >= 0");
            });

        migrationBuilder.CreateTable(
            name: "ledger_entries",
            columns: table => new
            {
                ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                ledger_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ledger_entries", x => x.ledger_entry_id);
                table.CheckConstraint("ck_ledger_entries_amount_positive", "amount > 0");
                table.ForeignKey(
                    name: "FK_ledger_entries_ledger_transactions_ledger_transaction_id",
                    column: x => x.ledger_transaction_id,
                    principalTable: "ledger_transactions",
                    principalColumn: "ledger_transaction_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ledger_entries_ledger_transaction_id",
            table: "ledger_entries",
            column: "ledger_transaction_id");

        migrationBuilder.CreateIndex(
            name: "IX_ledger_transactions_business_identity",
            table: "ledger_transactions",
            columns: new[] { "operation_type", "business_operation_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payments_order_id",
            table: "payments",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_provider_operations_business_identity",
            table: "provider_operations",
            columns: new[] { "operation_type", "business_operation_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_provider_operations_provider_idempotency_key",
            table: "provider_operations",
            column: "provider_idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_provider_operations_reconciliation",
            table: "provider_operations",
            columns: new[] { "status", "next_attempt_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ledger_entries");
        migrationBuilder.DropTable(name: "payments");
        migrationBuilder.DropTable(name: "provider_operations");
        migrationBuilder.DropTable(name: "ledger_transactions");
    }
}
