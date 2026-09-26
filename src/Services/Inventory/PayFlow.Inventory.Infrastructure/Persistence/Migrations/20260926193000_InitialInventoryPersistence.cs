using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Inventory.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Inventory.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260926193000_InitialInventoryPersistence")]
public partial class InitialInventoryPersistence
    : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_reservations",
            columns: table => new
            {
                reservation_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                order_id = table.Column<Guid>(
                    type: "uuid",
                    nullable: false),
                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                rejection_reason_code = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                version = table.Column<long>(
                    type: "bigint",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_inventory_reservations",
                    x => x.reservation_id);

                table.CheckConstraint(
                    "ck_inventory_reservations_expiry_after_creation",
                    "expires_at_utc > created_at_utc");

                table.CheckConstraint(
                    "ck_inventory_reservations_rejection_reason",
                    "(status = 'Rejected' AND rejection_reason_code IS NOT NULL) OR "
                    + "(status <> 'Rejected' AND rejection_reason_code IS NULL)");
            });

        migrationBuilder.CreateTable(
            name: "inventory_reservation_items",
            columns: table => new
            {
                reservation_id = table.Column<Guid>(
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
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_inventory_reservation_items",
                    x => new
                    {
                        x.reservation_id,
                        x.position
                    });

                table.CheckConstraint(
                    "ck_inventory_reservation_items_quantity_positive",
                    "quantity > 0");

                table.ForeignKey(
                    name: "FK_inventory_reservation_items_inventory_reservations_reservation_id",
                    column: x => x.reservation_id,
                    principalTable: "inventory_reservations",
                    principalColumn: "reservation_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_inventory_reservations_order_id",
            table: "inventory_reservations",
            column: "order_id");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inventory_reservation_items");

        migrationBuilder.DropTable(
            name: "inventory_reservations");
    }
}
