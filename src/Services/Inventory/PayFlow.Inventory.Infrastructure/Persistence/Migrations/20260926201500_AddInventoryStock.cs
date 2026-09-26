using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Inventory.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Inventory.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260926201500_AddInventoryStock")]
public partial class AddInventoryStock
    : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_stock",
            columns: table => new
            {
                sku_id = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                on_hand = table.Column<int>(
                    type: "integer",
                    nullable: false),
                reserved = table.Column<int>(
                    type: "integer",
                    nullable: false),
                version = table.Column<long>(
                    type: "bigint",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_inventory_stock",
                    x => x.sku_id);

                table.CheckConstraint(
                    "ck_inventory_stock_on_hand_non_negative",
                    "on_hand >= 0");

                table.CheckConstraint(
                    "ck_inventory_stock_reserved_range",
                    "reserved >= 0 AND reserved <= on_hand");
            });
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inventory_stock");
    }
}
