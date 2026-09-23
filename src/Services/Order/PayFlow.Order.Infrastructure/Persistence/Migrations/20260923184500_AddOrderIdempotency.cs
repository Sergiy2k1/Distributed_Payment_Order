using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Order.Infrastructure.Persistence.Migrations;

public partial class AddOrderIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "order_idempotency",
            columns: table => new
            {
                idempotency_key = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                request_hash = table.Column<string>(
                    type: "character(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: false),
                order_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                response_status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                response_total_amount = table.Column<decimal>(
                    type: "numeric(19,4)",
                    precision: 19,
                    scale: 4,
                    nullable: false),
                response_currency = table.Column<string>(
                    type: "character(3)",
                    fixedLength: true,
                    maxLength: 3,
                    nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_order_idempotency",
                    x => x.idempotency_key);

                table.ForeignKey(
                    name: "FK_order_idempotency_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_idempotency_order_id",
            table: "order_idempotency",
            column: "order_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_idempotency");
    }
}
