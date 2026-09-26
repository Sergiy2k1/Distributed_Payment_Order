using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260926112000_AddCheckoutSagaState")]
public partial class AddCheckoutSagaState : Migration
{
    private static readonly string[] RecoveryIndexColumns =
    [
        "status",
        "next_attempt_at_utc"
    ];

    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "checkout_sagas",
            columns: table => new
            {
                order_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                customer_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                status = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                currency = table.Column<string>(
                    type: "character(3)",
                    fixedLength: true,
                    maxLength: 3,
                    nullable: false),
                total_amount = table.Column<decimal>(
                    type: "numeric(19,4)",
                    precision: 19,
                    scale: 4,
                    nullable: false),
                started_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                deadline_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                retry_count = table.Column<int>(
                    type: "integer",
                    nullable: false),
                next_attempt_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                last_technical_error_code = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                last_technical_error_message = table.Column<string>(
                    type: "character varying(2048)",
                    maxLength: 2048,
                    nullable: true),
                version = table.Column<long>(
                    type: "bigint",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_checkout_sagas",
                    x => x.order_id);

                table.CheckConstraint(
                    "ck_checkout_sagas_deadline_after_start",
                    "deadline_at_utc > started_at_utc");

                table.CheckConstraint(
                    "ck_checkout_sagas_retry_count_non_negative",
                    "retry_count >= 0");

                table.CheckConstraint(
                    "ck_checkout_sagas_total_amount_positive",
                    "total_amount > 0");
            });

        migrationBuilder.CreateTable(
            name: "checkout_saga_items",
            columns: table => new
            {
                order_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                position = table.Column<int>(
                    type: "integer",
                    nullable: false),
                sku_id = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                quantity = table.Column<int>(
                    type: "integer",
                    nullable: false),
                unit_price = table.Column<decimal>(
                    type: "numeric(19,4)",
                    precision: 19,
                    scale: 4,
                    nullable: false),
                currency = table.Column<string>(
                    type: "character(3)",
                    fixedLength: true,
                    maxLength: 3,
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_checkout_saga_items",
                    x => new
                    {
                        x.order_id,
                        x.position
                    });

                table.CheckConstraint(
                    "ck_checkout_saga_items_quantity_positive",
                    "quantity > 0");

                table.CheckConstraint(
                    "ck_checkout_saga_items_unit_price_positive",
                    "unit_price > 0");

                table.ForeignKey(
                    name: "FK_checkout_saga_items_checkout_sagas_order_id",
                    column: x => x.order_id,
                    principalTable: "checkout_sagas",
                    principalColumn: "order_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_checkout_sagas_status_next_attempt_at_utc",
            table: "checkout_sagas",
            columns: RecoveryIndexColumns);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "checkout_saga_items");

        migrationBuilder.DropTable(
            name: "checkout_sagas");
    }
}
