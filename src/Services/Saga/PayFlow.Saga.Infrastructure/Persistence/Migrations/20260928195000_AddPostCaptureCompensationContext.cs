using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260928195000_AddPostCaptureCompensationContext")]
public partial class AddPostCaptureCompensationContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "refund_id",
            table: "checkout_sagas",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "post_capture_compensation_mode",
            table: "checkout_sagas",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "restock_operation_id",
            table: "checkout_sagas",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_checkout_sagas_post_capture_compensation_context",
            table: "checkout_sagas",
            sql: "(refund_id IS NULL AND post_capture_compensation_mode IS NULL AND restock_operation_id IS NULL) OR "
                 + "(refund_id IS NOT NULL AND post_capture_compensation_mode IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_checkout_sagas_post_capture_compensation_context",
            table: "checkout_sagas");

        migrationBuilder.DropColumn(
            name: "refund_id",
            table: "checkout_sagas");

        migrationBuilder.DropColumn(
            name: "post_capture_compensation_mode",
            table: "checkout_sagas");

        migrationBuilder.DropColumn(
            name: "restock_operation_id",
            table: "checkout_sagas");
    }
}
