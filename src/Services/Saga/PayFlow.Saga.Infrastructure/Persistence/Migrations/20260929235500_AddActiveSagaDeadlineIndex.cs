using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260929235500_AddActiveSagaDeadlineIndex")]
public partial class AddActiveSagaDeadlineIndex : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_checkout_sagas_active_deadline_at_utc",
            table: "checkout_sagas",
            column: "deadline_at_utc",
            filter: "status NOT IN ('Completed', 'CompletedWithBusinessFailure', 'ManualInterventionRequired')");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_checkout_sagas_active_deadline_at_utc",
            table: "checkout_sagas");
    }
}
