using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayFlow.Saga.Infrastructure.Persistence;

#nullable disable

namespace PayFlow.Saga.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SagaDbContext))]
[Migration("20260926101000_MakeInboxProcessedAtNullable")]
public partial class MakeInboxProcessedAtNullable : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "processed_at_utc",
            table: "inbox_messages",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone");
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE inbox_messages
            SET processed_at_utc = received_at_utc
            WHERE processed_at_utc IS NULL;
            """);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "processed_at_utc",
            table: "inbox_messages",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);
    }
}
