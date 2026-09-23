using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Order.Infrastructure.Persistence.Migrations;

public partial class InitialOrderSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                customer_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                total_amount = table.Column<decimal>(
                    type: "numeric(19,4)",
                    precision: 19,
                    scale: 4,
                    nullable: false),
                currency = table.Column<string>(
                    type: "character(3)",
                    fixedLength: true,
                    maxLength: 3,
                    nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                version = table.Column<long>(
                    type: "bigint",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            columns: table => new
            {
                order_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                position = table.Column<int>(
                    type: "integer",
                    nullable: false),
                sku = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                quantity = table.Column<int>(
                    type: "integer",
                    nullable: false),
                unit_price_amount = table.Column<decimal>(
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
                    "PK_order_items",
                    x => new { x.order_id, x.position });

                table.CheckConstraint(
                    "ck_order_items_quantity_positive",
                    "quantity > 0");

                table.CheckConstraint(
                    "ck_order_items_unit_price_positive",
                    "unit_price_amount > 0");

                table.ForeignKey(
                    name: "FK_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_items");

        migrationBuilder.DropTable(
            name: "orders");
    }
}
