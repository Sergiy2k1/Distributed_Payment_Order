using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Inventory.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Inventory.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260929000500_AddInventoryRestockOperation")]
public partial class AddInventoryRestockOperation : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "restock_operation_id",
            table: "inventory_reservations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_inventory_reservations_restock_operation",
            table: "inventory_reservations",
            sql: "(status = 'Restocked' AND restock_operation_id IS NOT NULL) OR "
                 + "(status <> 'Restocked' AND restock_operation_id IS NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_inventory_reservations_restock_operation_id",
            table: "inventory_reservations",
            column: "restock_operation_id",
            unique: true,
            filter: "restock_operation_id IS NOT NULL");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_inventory_reservations_restock_operation_id",
            table: "inventory_reservations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inventory_reservations_restock_operation",
            table: "inventory_reservations");

        migrationBuilder.DropColumn(
            name: "restock_operation_id",
            table: "inventory_reservations");
    }
}