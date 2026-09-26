using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260926184500_AddInventoryReservationState")]
public partial class AddInventoryReservationState : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "reservation_id",
            table: "checkout_sagas",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "reservation_expires_at_utc",
            table: "checkout_sagas",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_checkout_sagas_reservation_pair",
            table: "checkout_sagas",
            sql: "(reservation_id IS NULL AND reservation_expires_at_utc IS NULL) OR "
                + "(reservation_id IS NOT NULL AND reservation_expires_at_utc IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_checkout_sagas_reservation_deadline",
            table: "checkout_sagas",
            sql: "reservation_expires_at_utc IS NULL OR "
                + "(reservation_expires_at_utc > started_at_utc AND reservation_expires_at_utc <= deadline_at_utc)");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_checkout_sagas_reservation_deadline",
            table: "checkout_sagas");

        migrationBuilder.DropCheckConstraint(
            name: "ck_checkout_sagas_reservation_pair",
            table: "checkout_sagas");

        migrationBuilder.DropColumn(
            name: "reservation_expires_at_utc",
            table: "checkout_sagas");

        migrationBuilder.DropColumn(
            name: "reservation_id",
            table: "checkout_sagas");
    }
}
