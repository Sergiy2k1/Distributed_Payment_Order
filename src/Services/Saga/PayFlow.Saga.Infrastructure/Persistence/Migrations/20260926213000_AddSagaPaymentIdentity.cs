using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260926213000_AddSagaPaymentIdentity")]
public partial class AddSagaPaymentIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "payment_id",
            table: "checkout_sagas",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "payment_id",
            table: "checkout_sagas");
    }
}
